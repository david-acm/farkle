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

@secure()
@description('JWT signing key (Auth:JwtSecret) the app uses to sign/validate tokens.')
param jwtSecret string

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
    jwtSecret: jwtSecret
  }
}

@description('Public FQDN of the deployed WebApp.')
output webAppFqdn string = workload.outputs.webAppFqdn

@description('Login server of the container registry to push the WebApp image to.')
output containerRegistryLoginServer string = workload.outputs.containerRegistryLoginServer
