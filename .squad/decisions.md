# Squad Decisions

## Active Decisions

### Add Keycloak Container to Aspire AppHost (2026-03-21)

**Author:** Tank (Backend Dev)

**Context:** Auth system supports Entra/Auth0/Keycloak via config, but no local OIDC testing on `main` without external IdP.

**Decision:** Added `Aspire.Hosting.Keycloak` (preview 13.1.2-preview.1.26125.13) to AppHost. `dotnet run --project AppHost/AppHost` now spins up Keycloak on port 8080.

**Trade-offs:**
- Preview package (may need version bumps as Aspire 13.x stabilizes)
- Port 8080 reserved (adjustable in AppHost.cs)
- Realm setup is manual (bethuya realm + client creation in admin)

**Impact:** Auth docs updated in README; SECURITY.md refreshed; all tests pass.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

## Standing policies

The following policy documents are canonical and must be followed by all agents
and contributors:

- Rendering policy (Web only): `.squad/policies/rendering-policy.md`
- Shared RCL boundaries: `.squad/policies/rcl-boundaries.md`
