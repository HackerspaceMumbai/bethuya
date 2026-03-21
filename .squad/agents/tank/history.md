# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Learnings

- Bethuya uses central package management in `Directory.Packages.props`, warnings-as-errors, and shared auth abstractions in `Bethuya.Hybrid.Shared`.
- Auth is fully provider-pluggable on `main` via `Authentication:Provider` (None/Entra/Auth0/Keycloak). Code lives in `ServiceDefaults/Auth/` (extensions, options, provider enum) and `src/Bethuya.Hybrid/Bethuya.Hybrid.Web/Auth/` (state providers, user service).
- `Aspire.Hosting.Keycloak` is preview-only for Aspire 13.x. Used version `13.1.2-preview.1.26125.13` — pinned in `Directory.Packages.props`.
- Keycloak container added to AppHost on stable port `8080` (avoids browser cookie/OIDC token issues). Wired as a reference to both `web` and `backend`.
- Role claim types differ by provider: Entra=`roles`, Auth0=`https://bethuya.dev/roles`, Keycloak=`realm_access`. Mapped in `BethuyaAuthenticationExtensions.cs`.
- README repository structure was stale (referenced old `aspire/`, `Hackmum.Bethuya.App` paths). Updated to reflect actual layout.
- Key auth files: `AuthProviderType.cs`, `BethuyaAuthOptions.cs`, `BethuyaAuthenticationExtensions.cs`, `BethuyaAuthorizationExtensions.cs` in `ServiceDefaults/Auth/`.
