# Skill: seed-db

## Description
Seed development data into the Bethuya Backend API. Creates sample events, registrations, and attendee profiles for local development and testing.

> ⚠️ **Prerequisite:** The `Bethuya.Backend` project must be scaffolded and registered in the AppHost before this skill can run. If it doesn't exist yet, scaffold it first using `/scaffold-agent` or by creating the project manually.

## Trigger
Use when you need realistic data in the local dev environment. Invoke with `/seed-db` in Copilot CLI.

## Prerequisites
- Aspire AppHost running (`dotnet run --project AppHost/AppHost`)
- `Bethuya.Backend` project registered as an Aspire resource
- `Bogus` NuGet package added to `Directory.Packages.props` (version ≥ 35.x) for deterministic fake data

## Steps
1. Verify the Aspire AppHost is running and the `bethuya-backend` resource is healthy.
2. Resolve the Backend URL from Aspire service discovery (do NOT hardcode `localhost` — use the Aspire Dashboard or `IServiceEndpointResolver` to get the current port).
3. Call the Backend API seed endpoint: `POST /api/seed` with the desired scenario body.
4. Confirm data was created by querying `GET /api/events`.
5. Report the seeded entity counts.

## Parameters
- `--scenario` — data scenario to seed:
  - `oversubscribed-event` (default) — 1 event, capacity 50, 150 registrations, 3 speakers
  - `past-event` — completed event with feedback data
  - `multi-event` — 3 concurrent events at varying capacity
- `--count` — number of registrants to create (default: 150)

## Expected Output
```
Seeded: 1 event, 150 registrations (capacity: 50), 3 speakers.
Backend API: http://localhost:{aspire-assigned-port}
```

## Notes
- Seed data is deterministic — same parameters produce the same data (use `Bogus` with a fixed seed).
- Do not run against production.
- The Aspire Dashboard (`http://localhost:15888`) shows all resource URLs and health status.

