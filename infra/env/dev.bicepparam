using '../main.bicep'

param location = readEnvironmentVariable('AZURE_LOCATION', 'eastus')
param resourceGroupName = readEnvironmentVariable('AZURE_RESOURCE_GROUP', 'hotdice-dev-rg')
param environmentName = 'dev'
param namePrefix = 'hotdice'

// Microsoft CAF resource-type abbreviations used to build resource names.
// https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations
param resourceAbbreviations = {
  logAnalytics: 'log'
  applicationInsights: 'appi'
  postgreSql: 'psql'
  storageAccount: 'st'
  containerAppsEnvironment: 'cae'
  containerApp: 'ca'
}

// Supplied by CI / the operator at deploy time (kept out of source).
param imageTag = readEnvironmentVariable('IMAGE_TAG', 'latest')
param postgresAdminLogin = 'farkleadmin'
param postgresAdminPassword = readEnvironmentVariable('PG_ADMIN_PASSWORD', '')

// References to the persistent stack (resolved from its deployment outputs by the CD workflow).
param acrLoginServer = readEnvironmentVariable('ACR_LOGIN_SERVER', '')
param keyVaultUri = readEnvironmentVariable('KEY_VAULT_URI', '')
param identityResourceId = readEnvironmentVariable('IDENTITY_RESOURCE_ID', '')
param identityPrincipalId = readEnvironmentVariable('IDENTITY_PRINCIPAL_ID', '')
param identityClientId = readEnvironmentVariable('IDENTITY_CLIENT_ID', '')
param identityName = readEnvironmentVariable('IDENTITY_NAME', '')

// Cost control. Set BUDGET_ALERT_EMAILS to a comma-free single address, or edit here.
param monthlyBudgetAmount = 50
param budgetThresholds = [ 80, 100 ]
param budgetAlertEmails = [ readEnvironmentVariable('BUDGET_ALERT_EMAIL', 'changeme@example.com') ]
