@description('Azure region for all resources.')
param location string

@description('Short environment name used in resource names, e.g. dev or prod.')
param environmentName string

@description('Naming prefix for all resources.')
param namePrefix string

@description('Microsoft Cloud Adoption Framework resource-type abbreviations used to build resource names. Defaults follow https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations — override in the .bicepparam to use your own.')
param resourceAbbreviations object = {
  logAnalytics: 'log'
  postgreSql: 'psql'
  storageAccount: 'st'
  containerAppsEnvironment: 'cae'
  containerApp: 'ca'
}

@description('Container image tag for the WebApp image (e.g. a git SHA or "latest").')
param imageTag string

@description('PostgreSQL administrator login.')
param postgresAdminLogin string

@secure()
@description('PostgreSQL administrator password.')
param postgresAdminPassword string

@description('Login server of the persistent container registry the WebApp pulls from.')
param acrLoginServer string

@description('URI of the persistent Key Vault holding the jwt-secret.')
param keyVaultUri string

@description('Resource ID of the persistent user-assigned identity (ACR pull + KV read).')
param identityResourceId string

@description('Monthly cost budget for the resource group, in the billing currency.')
param monthlyBudgetAmount int = 50

@description('Budget alert thresholds, as percentages of the monthly amount.')
param budgetThresholds array = [ 80, 100 ]

@description('Emails notified when a budget threshold is crossed.')
param budgetAlertEmails array = [ 'changeme@example.com' ]

// ---------------------------------------------------------------------------
// Naming. Resource names follow the CAF convention "<abbreviation>-<workload>-<env>",
// dropping the hyphens for the globally-unique storage account whose name rules
// forbid them (3-24 lowercase alphanumerics). A short deterministic suffix off the
// resource group + environment keeps the globally-unique names collision-free.
// ---------------------------------------------------------------------------
var suffix = take(uniqueString(resourceGroup().id, environmentName), 8)
var storageName = take(toLower('${resourceAbbreviations.storageAccount}${namePrefix}${suffix}'), 24)
var fileShareName = 'esdb-data'
var databaseName = 'farkle_identity'

var esdbAppName = '${resourceAbbreviations.containerApp}-${namePrefix}-esdb-${environmentName}'
var webAppName = '${resourceAbbreviations.containerApp}-${namePrefix}-web-${environmentName}'
var budgetName = '${namePrefix}-budget-${environmentName}'

// ESDB runs insecure inside the Container Apps environment; app-to-app TCP is
// addressed by the container app name on its exposed port.
var esdbConnectionString = 'esdb://${esdbAppName}:2113?tls=false'

// ---------------------------------------------------------------------------
// Observability + identity
// ---------------------------------------------------------------------------
module logAnalytics 'br/public:avm/res/operational-insights/workspace:0.15.1' = {
  name: 'log-analytics'
  params: {
    name: '${resourceAbbreviations.logAnalytics}-${namePrefix}-${environmentName}'
    location: location
  }
}

// ---------------------------------------------------------------------------
// Data: PostgreSQL Flexible Server (managed)
// ---------------------------------------------------------------------------
module postgres 'br/public:avm/res/db-for-postgre-sql/flexible-server:0.15.4' = {
  name: 'postgres'
  params: {
    name: '${resourceAbbreviations.postgreSql}-${namePrefix}-${environmentName}-${suffix}'
    location: location
    skuName: 'Standard_B1ms'
    tier: 'Burstable'
    availabilityZone: -1
    // Burstable SKUs don't support High Availability; the AVM module otherwise
    // defaults to zone-redundant HA, which fails with HANotSupportedForBurstableSku.
    highAvailability: 'Disabled'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    databases: [ { name: databaseName } ]
    firewallRules: [
      // Allow other Azure services (incl. Container Apps) to reach the server.
      { name: 'AllowAllAzureInternalIPs', startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
    ]
  }
}

// The Postgres connection string is assembled as a Container App value secret
// below. The jwt-secret + the registry + the identity live in the PERSISTENT
// stack (infra/persistent.bicep); this stack references them by the params above.
var postgresFqdn = postgres.outputs.?fqdn ?? ''

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
    name: '${resourceAbbreviations.containerAppsEnvironment}-${namePrefix}-${environmentName}'
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
      userAssignedResourceIds: [ identityResourceId ]
    }
    ingressExternal: true
    ingressTargetPort: 8080
    scaleSettings: { minReplicas: 1, maxReplicas: 3 }
    registries: [
      {
        server: acrLoginServer
        identity: identityResourceId
      }
    ]
    secrets: [
      {
        name: 'auth-jwtsecret'
        keyVaultUrl: '${keyVaultUri}secrets/jwt-secret'
        identity: identityResourceId
      }
      {
        // Assembled here (not via KV) because the Postgres FQDN is created in this
        // disposable RG, which the persistent Key Vault cannot recompute.
        name: 'connectionstrings-identity'
        value: 'Host=${postgresFqdn};Database=${databaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=true'
      }
      {
        name: 'connectionstrings-esdb'
        value: esdbConnectionString
      }
    ]
    containers: [
      {
        name: 'webapp'
        image: '${acrLoginServer}/webapp:${imageTag}'
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

// ---------------------------------------------------------------------------
// Cost control: a monthly budget that emails the configured recipients when
// spend crosses the thresholds. The on-overspend teardown is enforced by the
// infra-cost-guard GitHub Actions workflow, which polls this budget's current
// spend and deletes the resource group when it exceeds the amount.
// ---------------------------------------------------------------------------
module budget 'br/public:avm/res/consumption/budget/rg-scope:0.1.0' = {
  name: 'budget'
  params: {
    name: budgetName
    amount: monthlyBudgetAmount
    category: 'Cost'
    resetPeriod: 'Monthly'
    thresholds: budgetThresholds
    contactEmails: budgetAlertEmails
  }
}

@description('Public FQDN of the deployed WebApp.')
output webAppFqdn string = webApp.outputs.fqdn

@description('Name of the resource-group cost budget.')
output budgetName string = budgetName
