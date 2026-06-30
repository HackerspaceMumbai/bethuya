@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param bethuya_env_outputs_azure_container_apps_environment_default_domain string

param bethuya_env_outputs_azure_container_apps_environment_id string

param web_containerimage string

param web_containerport string

@secure()
param oauth_github_clientsecret string

@secure()
param oauth_linkedin_clientsecret string

param bethuya_env_outputs_azure_container_registry_endpoint string

param bethuya_env_outputs_azure_container_registry_managed_identity_id string

resource web 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'web'
  location: location
  properties: {
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: int(web_containerport)
        transport: 'http'
      }
      registries: [
        {
          server: bethuya_env_outputs_azure_container_registry_endpoint
          identity: bethuya_env_outputs_azure_container_registry_managed_identity_id
        }
      ]
      secrets: [
        {
          name: 'github-client-secret'
          value: oauth_github_clientsecret
        }
        {
          name: 'linkedin-client-secret'
          value: oauth_linkedin_clientsecret
        }
      ]
      runtime: {
        dotnet: {
          autoConfigureDataProtection: true
        }
      }
    }
    environmentId: bethuya_env_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: web_containerimage
          name: 'web'
          env: [
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'HTTP_PORTS'
              value: web_containerport
            }
            {
              name: 'HTTPS_PORTS'
              value: web_containerport
            }
            {
              name: 'BACKEND_HTTP'
              value: 'https://backend.internal.${bethuya_env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__backend__http__0'
              value: 'https://backend.internal.${bethuya_env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'BACKEND_HTTPS'
              value: 'https://backend.internal.${bethuya_env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__backend__https__0'
              value: 'https://backend.internal.${bethuya_env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'Onboarding__BypassSocialConnections'
              value: 'false'
            }
            {
              name: 'Onboarding__BypassMandatoryProfile'
              value: 'false'
            }
            {
              name: 'SocialConnections__GitHub__ClientId'
              value: 'Ov23liOoR9dMeN5uOQrs'
            }
            {
              name: 'SocialConnections__GitHub__ClientSecret'
              secretRef: 'github-client-secret'
            }
            {
              name: 'SocialConnections__GitHub__CallbackPath'
              value: '/oauth/github/callback'
            }
            {
              name: 'SocialConnections__LinkedIn__ClientId'
              value: '86rr1crp0npnmc'
            }
            {
              name: 'SocialConnections__LinkedIn__ClientSecret'
              secretRef: 'linkedin-client-secret'
            }
            {
              name: 'SocialConnections__LinkedIn__CallbackPath'
              value: '/oauth/linkedin/callback'
            }
            {
              name: 'SocialConnections__LinkedIn__Scopes__0'
              value: 'openid'
            }
            {
              name: 'SocialConnections__LinkedIn__Scopes__1'
              value: 'profile'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 5
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${bethuya_env_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}
