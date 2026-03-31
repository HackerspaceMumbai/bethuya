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
- Backend event endpoints hardened (2026-03-21): Added `EventResponse` DTO in `EventContracts.cs` to decouple API responses from domain entities. All GET/POST/PUT endpoints now return consistent DTOs with enums serialized as strings (Type, Status).
- Server-side validation added to POST/PUT `/api/events`: Title required + max 200 chars, Capacity 1-10,000, EndDate >= StartDate, CreatedBy required. Returns `Results.ValidationProblem()` with field-level errors.
- `JsonStringEnumConverter` registered in `Program.cs` (line 10-11) enables frontend to send `Type: "Meetup"` string, Backend deserializes to `EventType.Meetup` enum correctly.
- Key Backend files: `src/Hackmum.Bethuya.Backend/Endpoints/EventEndpoints.cs`, `src/Hackmum.Bethuya.Backend/Contracts/EventContracts.cs`, `src/Hackmum.Bethuya.Backend/Program.cs`.
- Frontend API contract: `src/Bethuya.Hybrid/Bethuya.Hybrid.Shared/Services/IEventApi.cs` defines `EventDto` and `CreateEventDto` for Refit client.
- Cloudinary image upload integrated (2026-03-21): Added `CloudinaryDotNet` 1.26.2 via Central Package Management. New `IImageUploadService` interface in Core, `CloudinaryImageUploadService` + `CloudinaryOptions` in Infrastructure, `ImageEndpoints` (POST /api/images/upload) in Backend. `CoverImageUrl` property added to Event model, all contracts (Create/Update/Response), EF config (max 2048), Refit DTOs, and endpoint mappers. Image upload validates file type (JPEG/PNG/WebP/GIF) and size (5 MB max). DI registered as singleton in `InfrastructureServiceExtensions`. Cloudinary credentials expected in `appsettings.json` under `Cloudinary` section (CloudName, ApiKey, ApiSecret).
