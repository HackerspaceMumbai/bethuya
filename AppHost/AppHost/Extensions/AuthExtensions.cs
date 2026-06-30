using System;
using System.Collections.Generic;
using System.Text;

namespace AppHost.Extensions;


using Aspire.Hosting.ApplicationModel;

public sealed record SocialAuthSettings(
    IResourceBuilder<ParameterResource> GitHubClientId,
    IResourceBuilder<ParameterResource> GitHubClientSecret,
    string GitHubCallbackPath,
    IResourceBuilder<ParameterResource> LinkedInClientId,
    IResourceBuilder<ParameterResource> LinkedInClientSecret,
    string LinkedInCallbackPath,
    IResourceBuilder<ParameterResource> LinkedInScope0,
    IResourceBuilder<ParameterResource> LinkedInScope1);

public static  class AuthExtensions
{
   

public static IResourceBuilder<T> ConfigureSocialAuth<T>(
        this IResourceBuilder<T> project,
        SocialAuthSettings settings)
        where T : ProjectResource
    {
        project
            .WithEnvironment(
                "SocialConnections__GitHub__ClientId",
                settings.GitHubClientId)

            .WithEnvironment(
                "SocialConnections__GitHub__ClientSecret",
                settings.GitHubClientSecret)

            .WithEnvironment(
                "SocialConnections__GitHub__CallbackPath",
                settings.GitHubCallbackPath)

            .WithEnvironment(
                "SocialConnections__LinkedIn__ClientId",
                settings.LinkedInClientId)

            .WithEnvironment(
                "SocialConnections__LinkedIn__ClientSecret",
                settings.LinkedInClientSecret)

            .WithEnvironment(
                "SocialConnections__LinkedIn__CallbackPath",
                settings.LinkedInCallbackPath)

            .WithEnvironment(
                "SocialConnections__LinkedIn__Scopes__0",
                settings.LinkedInScope0)

            .WithEnvironment(
                "SocialConnections__LinkedIn__Scopes__1",
                settings.LinkedInScope1);

        return project;
    }

}
