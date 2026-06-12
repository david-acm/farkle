using '../persistent.bicep'

param location = 'eastus'
param resourceGroupName = readEnvironmentVariable('AZURE_PERSISTENT_RESOURCE_GROUP', 'hotdice-shared-rg')
param environmentName = 'shared'
param namePrefix = 'hotdice'

// Microsoft CAF resource-type abbreviations used to build resource names.
// https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations
param resourceAbbreviations = {
  containerRegistry: 'cr'
  keyVault: 'kv'
  managedIdentity: 'id'
}

// Supplied by CI / the operator at deploy time (kept out of source).
param jwtSecret = readEnvironmentVariable('JWT_SECRET', '')
