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

// Cost control. Set BUDGET_ALERT_EMAILS to a comma-free single address, or edit here.
param monthlyBudgetAmount = 200
param budgetThresholds = [ 80, 100 ]
param budgetAlertEmails = [ readEnvironmentVariable('BUDGET_ALERT_EMAIL', 'changeme@example.com') ]
