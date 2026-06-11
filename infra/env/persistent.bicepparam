using '../persistent.bicep'

param location = 'eastus'
param resourceGroupName = readEnvironmentVariable('AZURE_PERSISTENT_RESOURCE_GROUP', 'farkle-shared-rg')
param environmentName = 'shared'
param namePrefix = 'farkle'

// Supplied by CI / the operator at deploy time (kept out of source).
param jwtSecret = readEnvironmentVariable('JWT_SECRET', '')
