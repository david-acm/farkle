@description('Azure region for all resources.')
param location string

@description('Short environment name used in resource names, e.g. dev or prod.')
param environmentName string

@description('Naming prefix for all resources.')
param namePrefix string

@description('Container image tag for the WebApp image in ACR.')
param imageTag string

@description('PostgreSQL administrator login.')
param postgresAdminLogin string

@secure()
@description('PostgreSQL administrator password.')
param postgresAdminPassword string

@secure()
@description('JWT signing key (Auth:JwtSecret).')
param jwtSecret string

// ---------------------------------------------------------------------------
// Naming. ACR / Key Vault / Storage names must be globally unique; derive a
// short deterministic suffix from the resource group + environment.
// ---------------------------------------------------------------------------
var suffix = take(uniqueString(resourceGroup().id, environmentName), 8)
var acrName = toLower('${namePrefix}acr${suffix}')
var keyVaultName = take(toLower('${namePrefix}kv${suffix}'), 24)
var storageName = take(toLower('${namePrefix}st${suffix}'), 24)
var fileShareName = 'esdb-data'
var databaseName = 'farkle_identity'

var esdbAppName = '${namePrefix}-esdb-${environmentName}'
var webAppName = '${namePrefix}-web-${environmentName}'

// ESDB runs insecure inside the Container Apps environment; app-to-app TCP is
// addressed by the container app name on its exposed port.
var esdbConnectionString = 'esdb://${esdbAppName}:2113?tls=false'

// ---------------------------------------------------------------------------
// Observability + identity
// ---------------------------------------------------------------------------
module logAnalytics 'br/public:avm/res/operational-insights/workspace:0.15.1' = {
  name: 'log-analytics'
  params: {
    name: '${namePrefix}-log-${environmentName}'
    location: location
  }
}

module identity 'br/public:avm/res/managed-identity/user-assigned-identity:0.5.1' = {
  name: 'identity'
  params: {
    name: '${namePrefix}-id-${environmentName}'
    location: location
  }
}

// ---------------------------------------------------------------------------
// Data: PostgreSQL Flexible Server (managed)
// ---------------------------------------------------------------------------
module postgres 'br/public:avm/res/db-for-postgre-sql/flexible-server:0.15.4' = {
  name: 'postgres'
  params: {
    name: '${namePrefix}-pg-${environmentName}-${suffix}'
    location: location
    skuName: 'Standard_B1ms'
    tier: 'Burstable'
    availabilityZone: -1
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    databases: [ { name: databaseName } ]
    firewallRules: [
      // Allow other Azure services (incl. Container Apps) to reach the server.
      { name: 'AllowAllAzureInternalIPs', startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
    ]
  }
}

// ---------------------------------------------------------------------------
// Secrets: Key Vault holds the Postgres connection string + JWT secret; the
// user-assigned identity gets Key Vault Secrets User so the WebApp can read them.
// ---------------------------------------------------------------------------
var postgresFqdn = postgres.outputs.?fqdn ?? ''

module keyVault 'br/public:avm/res/key-vault/vault:0.13.3' = {
  name: 'key-vault'
  params: {
    name: keyVaultName
    location: location
    enableRbacAuthorization: true
    enablePurgeProtection: false
    secrets: [
      { name: 'jwt-secret', value: jwtSecret }
      { name: 'postgres-admin-password', value: postgresAdminPassword }
      {
        name: 'connectionstrings-identity'
        value: 'Host=${postgresFqdn};Database=${databaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=true'
      }
    ]
    roleAssignments: [
      {
        principalId: identity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: 'Key Vault Secrets User'
      }
    ]
  }
}

// ---------------------------------------------------------------------------
// Registry: the WebApp image is pushed here by CI; identity gets AcrPull.
// ---------------------------------------------------------------------------
module acr 'br/public:avm/res/container-registry/registry:0.12.1' = {
  name: 'acr'
  params: {
    name: acrName
    location: location
    acrSku: 'Basic'
    roleAssignments: [
      {
        principalId: identity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: 'AcrPull'
      }
    ]
  }
}

// ---------------------------------------------------------------------------
// Storage: Azure Files share that backs the EventStore data volume.
// ---------------------------------------------------------------------------
module storage 'br/public:avm/res/storage/storage-account:0.32.1' = {
  name: 'storage'
  params: {
    name: storageName
    location: location
    skuName: 'Standard_LRS'
    fileServices: {
      shares: [ { name: fileShareName } ]
    }
  }
}

// ---------------------------------------------------------------------------
// Container Apps environment + the Azure Files storage definition for ESDB.
// AVM looks up the storage account key from storageAccountName internally; the
// env storage `name` doubles as the file share name (esdb-data).
// ---------------------------------------------------------------------------
module containerEnv 'br/public:avm/res/app/managed-environment:0.13.3' = {
  name: 'container-env'
  dependsOn: [ storage ]
  params: {
    name: '${namePrefix}-cae-${environmentName}'
    location: location
    zoneRedundant: false
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsWorkspaceResourceId: logAnalytics.outputs.resourceId
    }
    storages: [
      {
        name: fileShareName
        accessMode: 'ReadWrite'
        kind: 'SMB'
        storageAccountName: storageName
      }
    ]
  }
}

// ---------------------------------------------------------------------------
// EventStore container app — internal TCP on 2113, persistent Azure Files volume.
// ---------------------------------------------------------------------------
module esdbApp 'br/public:avm/res/app/container-app:0.22.1' = {
  name: 'esdb-app'
  params: {
    name: esdbAppName
    location: location
    environmentResourceId: containerEnv.outputs.resourceId
    ingressExternal: false
    ingressTargetPort: 2113
    ingressTransport: 'tcp'
    ingressAllowInsecure: true
    scaleSettings: { minReplicas: 1, maxReplicas: 1 }
    volumes: [
      {
        name: fileShareName
        storageType: 'AzureFile'
        storageName: fileShareName
      }
    ]
    containers: [
      {
        name: 'esdb'
        image: 'eventstore/eventstore:23.10.0-bookworm-slim'
        resources: { cpu: json('0.5'), memory: '1Gi' }
        env: [
          { name: 'EVENTSTORE_INSECURE', value: 'true' }
          { name: 'EVENTSTORE_CLUSTER_SIZE', value: '1' }
          { name: 'EVENTSTORE_RUN_PROJECTIONS', value: 'All' }
          { name: 'EVENTSTORE_START_STANDARD_PROJECTIONS', value: 'true' }
          { name: 'EVENTSTORE_HTTP_PORT', value: '2113' }
          { name: 'EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP', value: 'true' }
        ]
        volumeMounts: [
          { volumeName: fileShareName, mountPath: '/var/lib/eventstore' }
        ]
      }
    ]
  }
}

// ---------------------------------------------------------------------------
// WebApp container app — external HTTPS on 8080, KV-referenced secrets, probes.
// ---------------------------------------------------------------------------
module webApp 'br/public:avm/res/app/container-app:0.22.1' = {
  name: 'webapp-app'
  params: {
    name: webAppName
    location: location
    environmentResourceId: containerEnv.outputs.resourceId
    managedIdentities: {
      userAssignedResourceIds: [ identity.outputs.resourceId ]
    }
    ingressExternal: true
    ingressTargetPort: 8080
    scaleSettings: { minReplicas: 1, maxReplicas: 3 }
    registries: [
      {
        server: acr.outputs.loginServer
        identity: identity.outputs.resourceId
      }
    ]
    secrets: [
      {
        name: 'auth-jwtsecret'
        keyVaultUrl: '${keyVault.outputs.uri}secrets/jwt-secret'
        identity: identity.outputs.resourceId
      }
      {
        name: 'connectionstrings-identity'
        keyVaultUrl: '${keyVault.outputs.uri}secrets/connectionstrings-identity'
        identity: identity.outputs.resourceId
      }
      {
        name: 'connectionstrings-esdb'
        value: esdbConnectionString
      }
    ]
    containers: [
      {
        name: 'webapp'
        image: '${acr.outputs.loginServer}/webapp:${imageTag}'
        resources: { cpu: json('0.5'), memory: '1Gi' }
        env: [
          { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
          { name: 'Auth__RequireAuthorization', value: 'true' }
          { name: 'Auth__JwtSecret', secretRef: 'auth-jwtsecret' }
          { name: 'ConnectionStrings__Identity', secretRef: 'connectionstrings-identity' }
          { name: 'ConnectionStrings__Esdb', secretRef: 'connectionstrings-esdb' }
        ]
        probes: [
          {
            type: 'Liveness'
            httpGet: { path: '/health/live', port: 8080 }
            initialDelaySeconds: 15
            periodSeconds: 30
          }
          {
            type: 'Readiness'
            httpGet: { path: '/health/ready', port: 8080 }
            initialDelaySeconds: 15
            periodSeconds: 30
            failureThreshold: 6
          }
        ]
      }
    ]
  }
}

@description('Public FQDN of the deployed WebApp.')
output webAppFqdn string = webApp.outputs.fqdn

@description('Login server of the container registry.')
output containerRegistryLoginServer string = acr.outputs.loginServer
