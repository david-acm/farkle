targetScope = 'subscription'

@description('Azure region for all resources.')
param location string

@description('Resource group to create (or reuse) for the deployment.')
param resourceGroupName string

@description('Short environment name used in resource names, e.g. dev or prod.')
param environmentName string

@description('Naming prefix for all resources.')
param namePrefix string = 'farkle'

@description('Container image tag for the WebApp image in ACR (e.g. a git SHA).')
param imageTag string = 'latest'

@description('PostgreSQL administrator login.')
param postgresAdminLogin string = 'farkleadmin'

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

@description('Emails notified when a budget threshold is crossed. Override with real recipients.')
param budgetAlertEmails array = [ 'changeme@example.com' ]

resource rg 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
}

module workload 'modules/workload.bicep' = {
  scope: rg
  name: 'workload-${environmentName}'
  params: {
    location: location
    environmentName: environmentName
    namePrefix: namePrefix
    imageTag: imageTag
    postgresAdminLogin: postgresAdminLogin
    postgresAdminPassword: postgresAdminPassword
    acrLoginServer: acrLoginServer
    keyVaultUri: keyVaultUri
    identityResourceId: identityResourceId
    monthlyBudgetAmount: monthlyBudgetAmount
    budgetThresholds: budgetThresholds
    budgetAlertEmails: budgetAlertEmails
  }
}

@description('Public FQDN of the deployed WebApp.')
output webAppFqdn string = workload.outputs.webAppFqdn

@description('Name of the resource-group cost budget (used by the cost-guard automation).')
output budgetName string = workload.outputs.budgetName
