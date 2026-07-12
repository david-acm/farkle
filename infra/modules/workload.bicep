@description('Azure region for all resources.')
param location string

@description('Short environment name used in resource names, e.g. dev or prod.')
param environmentName string

@description('Naming prefix for all resources.')
param namePrefix string

@description('Microsoft Cloud Adoption Framework resource-type abbreviations used to build resource names. Defaults follow https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations — override in the .bicepparam to use your own.')
param resourceAbbreviations object = {
  logAnalytics: 'log'
  applicationInsights: 'appi'
  postgreSql: 'psql'
  storageAccount: 'st'
  containerAppsEnvironment: 'cae'
  containerApp: 'ca'
}

@description('Container image tag for the WebApp image (e.g. a git SHA or "latest").')
param imageTag string

@description('Changes every deployment so the WebApp rolls a NEW revision — Container Apps does not roll a revision on secret-value changes alone, so without this a changed connection string / image:latest would never reach the running app.')
param deploymentId string = utcNow()

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

@description('Principal (object) ID of the persistent user-assigned identity — registered as the Postgres Entra administrator.')
param identityPrincipalId string

@description('Client ID of the persistent user-assigned identity — the WebApp uses it (AZURE_CLIENT_ID) to fetch an Entra token for Postgres.')
param identityClientId string

@description('Name of the persistent user-assigned identity — the Postgres Entra username the WebApp connects as.')
param identityName string

@description('Monthly cost budget for the resource group, in the billing currency.')
param monthlyBudgetAmount int = 50

@description('Budget alert thresholds, as percentages of the monthly amount.')
param budgetThresholds array = [ 80, 100 ]

@description('Emails notified when a budget threshold is crossed.')
param budgetAlertEmails array = [ 'changeme@example.com' ]

// ---------------------------------------------------------------------------
// Naming. Resource names follow the CAF convention "<abbreviation>-<workload>-<env>".
// A short deterministic suffix off the resource group + environment keeps the
// globally-unique names (e.g. the Postgres server) collision-free.
// ---------------------------------------------------------------------------
var suffix = take(uniqueString(resourceGroup().id, environmentName), 8)
var databaseName = 'farkle_identity'

var webAppName = '${resourceAbbreviations.containerApp}-${namePrefix}-web-${environmentName}'
var budgetName = '${namePrefix}-budget-${environmentName}'

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

// #33 — workspace-based Application Insights for distributed tracing + the Serilog sink.
// Co-located with its Log Analytics workspace (both disposable), so the connection string is a
// deployment output injected directly into the WebApp env rather than a static Key Vault secret.
module applicationInsights 'br/public:avm/res/insights/component:0.6.0' = {
  name: 'application-insights'
  params: {
    name: '${resourceAbbreviations.applicationInsights}-${namePrefix}-${environmentName}'
    location: location
    workspaceResourceId: logAnalytics.outputs.resourceId
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
    // Password auth: the WebApp connects with the admin login/password above.
    // (Managed-identity/Entra token auth is implemented in WebApp.IdentityDataSource
    // and the bicep scaffolding remains, but is parked — the token call hung at
    // startup; re-enable by switching authConfig + dropping the password below.)
    authConfig: {
      activeDirectoryAuth: 'Disabled'
      passwordAuth: 'Enabled'
    }
    databases: [ { name: databaseName } ]
    // Public access ON so the firewall rules below actually apply; the AVM module
    // otherwise defaults to Disabled, leaving the server unreachable (firewall
    // rules can't be set on a public-access-disabled server).
    publicNetworkAccess: 'Enabled'
    firewallRules: [
      // The 0.0.0.0 rule is the special "allow Azure services" rule, which covers
      // the WebApp Container App's egress.
      { name: 'AllowAllAzureInternalIPs', startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
    ]
  }
}

// The Postgres connection string is assembled as a Container App value secret
// below. The jwt-secret + the registry + the identity live in the PERSISTENT
// stack (infra/persistent.bicep); this stack references them by the params above.
var postgresFqdn = postgres.outputs.?fqdn ?? ''

// ---------------------------------------------------------------------------
// Container Apps environment. Postgres is the single stateful store (ADR 0004):
// Marten's event store + Identity share the managed Postgres above, so there is
// no Azure Files share / storage account to mount anymore.
// ---------------------------------------------------------------------------
module containerEnv 'br/public:avm/res/app/managed-environment:0.13.3' = {
  name: 'container-env'
  params: {
    name: '${resourceAbbreviations.containerAppsEnvironment}-${namePrefix}-${environmentName}'
    location: location
    // Explicitly public: the WebApp uses external ingress. The AVM module otherwise
    // defaults publicNetworkAccess to Disabled (with no VNet), making the app
    // unreachable ("public network access on this managed environment is disabled").
    publicNetworkAccess: 'Enabled'
    zoneRedundant: false
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsWorkspaceResourceId: logAnalytics.outputs.resourceId
    }
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
    // New suffix each deploy → a fresh revision that re-reads secrets (e.g. the
    // Postgres connection string) and re-pulls image:latest.
    revisionSuffix: 'r${take(uniqueString(deploymentId), 12)}'
    environmentResourceId: containerEnv.outputs.resourceId
    managedIdentities: {
      userAssignedResourceIds: [ identityResourceId ]
    }
    ingressExternal: true
    ingressTargetPort: 8080
    // SignalR runs in-memory (no Redis/Azure SignalR backplane), so every player in a
    // game must land on the same replica or broadcasts/reconnects are lost (404 on the
    // hub, forced page reload). Pin to a single replica AND enable sticky sessions so a
    // reconnect returns to the same instance. Revisit (backplane + multi-replica) if we
    // need to scale out.
    scaleSettings: { minReplicas: 1, maxReplicas: 1 }
    stickySessionsAffinity: 'sticky'
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
        // Password auth: includes the admin login/password. The app's IdentityDataSource
        // uses plain password auth whenever the connection string carries a password.
        value: 'Host=${postgresFqdn};Database=${databaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=true'
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
          // Anonymous play: game endpoints are AllowAnonymous; players join by name
          // (the domain identifies players by name, no account needed). Flip to 'true'
          // to require a JWT for game actions.
          { name: 'Auth__RequireAuthorization', value: 'false' }
          { name: 'Auth__JwtSecret', secretRef: 'auth-jwtsecret' }
          { name: 'ConnectionStrings__Identity', secretRef: 'connectionstrings-identity' }
          // Tells DefaultAzureCredential which user-assigned identity to use for the Postgres token.
          { name: 'AZURE_CLIENT_ID', value: identityClientId }
          // #33 — Serilog reads this to enable the Application Insights sink (absent locally → console only).
          { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: applicationInsights.outputs.connectionString }
        ]
        // TCP probes (port-open) rather than HTTP /health/*: the health endpoints sit
        // behind auth and return 403, which would fail HTTP probes even when the app is
        // healthy. The generous liveness delay gives EF Core's startup migration time.
        probes: [
          {
            type: 'Liveness'
            tcpSocket: { port: 8080 }
            initialDelaySeconds: 60
            periodSeconds: 30
            failureThreshold: 5
          }
          {
            type: 'Readiness'
            tcpSocket: { port: 8080 }
            initialDelaySeconds: 20
            periodSeconds: 15
            failureThreshold: 10
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
