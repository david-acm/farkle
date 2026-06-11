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

@secure()
@description('JWT signing key (Auth:JwtSecret) stored in Key Vault.')
param jwtSecret string

// Registry / vault names must be globally unique; derive a short deterministic
// suffix from the (persistent) resource group + environment.
var suffix = take(uniqueString(resourceGroup().id, environmentName), 8)
var acrName = toLower('${namePrefix}acr${suffix}')
var keyVaultName = take(toLower('${namePrefix}kv${suffix}'), 24)

module identity 'br/public:avm/res/managed-identity/user-assigned-identity:0.5.1' = {
  name: 'identity'
  params: {
    name: '${namePrefix}-id-${environmentName}'
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
