using '../main.bicep'

param location = 'eastus'
param resourceGroupName = readEnvironmentVariable('AZURE_RESOURCE_GROUP', 'farkle-prod-rg')
param environmentName = 'prod'
param namePrefix = 'farkle'

// Supplied by CI / the operator at deploy time (kept out of source).
param imageTag = readEnvironmentVariable('IMAGE_TAG', 'latest')
param postgresAdminLogin = 'farkleadmin'
param postgresAdminPassword = readEnvironmentVariable('PG_ADMIN_PASSWORD', '')
param jwtSecret = readEnvironmentVariable('JWT_SECRET', '')
