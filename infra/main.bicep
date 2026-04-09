targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Prefix used to name Azure resources. Use lowercase letters and numbers only.')
@minLength(3)
@maxLength(18)
param namePrefix string

@description('SKU for the Linux App Service plan.')
@allowed([
  'B1'
  'S1'
  'P1v3'
])
param appServicePlanSku string = 'B1'

@description('Deployment environment name (dev/test/prod).')
@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string = 'dev'

var tags = {
  app: 'TryingStuff'
  environment: environmentName
  managedBy: 'bicep'
}

var webAppName = '${namePrefix}-api-${environmentName}'
var staticWebAppName = '${namePrefix}-web-${environmentName}'
var keyVaultName = '${namePrefix}kv${environmentName}'
var appInsightsName = '${namePrefix}-appi-${environmentName}'
var logAnalyticsName = '${namePrefix}-log-${environmentName}'
var appServicePlanName = '${namePrefix}-plan-${environmentName}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  tags: tags
  sku: {
    name: appServicePlanSku
    tier: appServicePlanSku == 'B1' ? 'Basic' : (appServicePlanSku == 'S1' ? 'Standard' : 'PremiumV3')
    size: appServicePlanSku
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServicePlanSku == 'B1' ? false : true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'AzureOpenAI__Endpoint'
          value: ''
        }
        {
          name: 'AzureOpenAI__ApiKey'
          value: ''
        }
        {
          name: 'AzureOpenAI__Model'
          value: 'gpt-4.1-mini'
        }
      ]
    }
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    enableRbacAuthorization: true
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    publicNetworkAccess: 'Enabled'
    enabledForTemplateDeployment: true
    softDeleteRetentionInDays: 90
  }
}

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    repositoryUrl: ''
    branch: 'main'
    stagingEnvironmentPolicy: 'Enabled'
    provider: 'GitHub'
    allowConfigFileUpdates: true
  }
}

output webAppName string = webApp.name
output webAppHostName string = webApp.properties.defaultHostName
output staticWebAppName string = staticWebApp.name
output staticWebAppDefaultHostName string = staticWebApp.properties.defaultHostname
output keyVaultName string = keyVault.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
