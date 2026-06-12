// ---------------------------------------------------------------------------
// Persistent platform resources (RG scope). These live in a resource group that
// the nightly teardown NEVER deletes, so the container image and the JWT secret
// survive the disposable compute/data being destroyed and recreated each cycle:
//   - Container Registry — holds the WebApp image (CI pushes here).
//   - Key Vault — holds the static jwt-secret (never soft-deleted, since it is
//     never destroyed — avoids the same-name soft-delete conflict on re-create).
//   - User-assigned identity — stable principalId, so its AcrPull / Key Vault
//     Secrets User role assignments never churn. The disposable WebApp references
//     this identity cross-RG to pull the image privately and read the secret.
// ---------------------------------------------------------------------------

@description('Azure region for all resources.')
param location string

@description('Short environment name used in resource names, e.g. shared.')
param environmentName string

@description('Naming prefix for all resources.')
param namePrefix string

@description('Microsoft Cloud Adoption Framework resource-type abbreviations used to build resource names. Defaults follow https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations — override in the .bicepparam to use your own.')
param resourceAbbreviations object = {
  containerRegistry: 'cr'
  keyVault: 'kv'
  managedIdentity: 'id'
}

@secure()
@description('JWT signing key (Auth:JwtSecret) stored in Key Vault.')
param jwtSecret string

// Resource names follow the CAF convention "<abbreviation>-<workload>-<env>",
// dropping the hyphens for the two globally-unique resources whose name rules
// forbid them / are length-capped (ACR: 5-50 alphanumerics; Key Vault: <=24).
// A short deterministic suffix off the (persistent) RG + environment keeps the
// globally-unique names collision-free.
var suffix = take(uniqueString(resourceGroup().id, environmentName), 8)
var acrName = toLower('${resourceAbbreviations.containerRegistry}${namePrefix}${suffix}')
var keyVaultName = take(toLower('${resourceAbbreviations.keyVault}${namePrefix}${suffix}'), 24)
var identityName = '${resourceAbbreviations.managedIdentity}-${namePrefix}-${environmentName}'

module identity 'br/public:avm/res/managed-identity/user-assigned-identity:0.5.1' = {
  name: 'identity'
  params: {
    name: identityName
    location: location
  }
}

module keyVault 'br/public:avm/res/key-vault/vault:0.13.3' = {
  name: 'key-vault'
  params: {
    name: keyVaultName
    location: location
    enableRbacAuthorization: true
    enablePurgeProtection: false
    secrets: [
      { name: 'jwt-secret', value: jwtSecret }
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

@description('Login server of the container registry (CI pushes here; the WebApp pulls from here).')
output acrLoginServer string = acr.outputs.loginServer

@description('Key Vault URI holding the jwt-secret.')
output keyVaultUri string = keyVault.outputs.uri

@description('Resource ID of the user-assigned identity (referenced by the disposable WebApp).')
output identityResourceId string = identity.outputs.resourceId

@description('Principal ID of the user-assigned identity.')
output identityPrincipalId string = identity.outputs.principalId

@description('Client ID of the user-assigned identity.')
output identityClientId string = identity.outputs.clientId
