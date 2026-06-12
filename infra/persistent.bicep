targetScope = 'subscription'

// Persistent platform stack: ACR + Key Vault + managed identity in a resource
// group the nightly teardown never touches. Deploy this once (and on changes);
// the disposable workload (main.bicep) references its outputs.

@description('Azure region for the persistent resources.')
param location string

@description('Persistent resource group to create (or reuse).')
param resourceGroupName string

@description('Short environment name used in resource names, e.g. shared.')
param environmentName string = 'shared'

@description('Naming prefix for all resources.')
param namePrefix string = 'farkle'

@description('Microsoft Cloud Adoption Framework resource-type abbreviations used to build resource names. Defaults follow https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations — override in the .bicepparam to use your own.')
param resourceAbbreviations object = {
  containerRegistry: 'cr'
  keyVault: 'kv'
  managedIdentity: 'id'
}

@secure()
@description('JWT signing key (Auth:JwtSecret) stored in Key Vault.')
param jwtSecret string

resource rg 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
}

module persistent 'modules/persistent.bicep' = {
  scope: rg
  name: 'persistent-${environmentName}'
  params: {
    location: location
    environmentName: environmentName
    namePrefix: namePrefix
    resourceAbbreviations: resourceAbbreviations
    jwtSecret: jwtSecret
  }
}

@description('Login server of the container registry.')
output acrLoginServer string = persistent.outputs.acrLoginServer

@description('Key Vault URI.')
output keyVaultUri string = persistent.outputs.keyVaultUri

@description('Resource ID of the user-assigned identity.')
output identityResourceId string = persistent.outputs.identityResourceId

@description('Principal ID of the user-assigned identity.')
output identityPrincipalId string = persistent.outputs.identityPrincipalId

@description('Client ID of the user-assigned identity.')
output identityClientId string = persistent.outputs.identityClientId
