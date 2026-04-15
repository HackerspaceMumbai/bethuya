# Squad Decisions Archive

Archived entries from decisions.md (older than 2026-04-08).

---

### 2026-03-31 — Performance Budget Directive

**Author:** Augustine Correa (via Copilot)

**Directive:** Ensure Playwright tests pass within project performance budgets:
- Hot path p99 < 180ms @ 2,500 RPS
- 0 B hot-path allocations
- > 90% cache hit rate

---

### E2E Test Selector Standards (2026-03-21)

**Author:** Switch (Tester)

**Decision:** All E2E Playwright tests MUST use `data-test` attributes as selectors instead of role-based or text-based selectors.

**Rationale:**
- Role-based selectors break when button text changes
- `data-test` provides stability decoupled from UI text and styling
- Explicit test contract between frontend and tests

**Standard Selectors:**
- `[data-test='new-event-btn']` — Opens create event dialog
- `[data-test='create-event-submit']` — Submits create form
- `[data-test='event-card']` — Individual event cards in list
- `[data-test='notification']` — Success/error notifications

**Implementation:** Trinity adds `data-test` attributes to interactive elements; Switch uses `Page.Locator("[data-test='selector']")` exclusively.

---

### Add Cloudinary Image Upload for Event Cover Pics (2026-03-21)

**Author:** Tank (Backend Dev)

**Decision:** Integrated **CloudinaryDotNet 1.26.2** as the image upload provider behind an `IImageUploadService` abstraction in Core. Implementation lives in Infrastructure (`CloudinaryImageUploadService`), configured via `CloudinaryOptions`.

**Changes:**
- `CoverImageUrl` (nullable, max 2048 chars) added to Event model, API contracts, EF config, Refit DTOs
- New `POST /api/images/upload` endpoint with validation: 5 MB max, JPEG/PNG/WebP/GIF only
- Images stored in `bethuya/events` folder; secure URL returned
- DI: `IImageUploadService` → `CloudinaryImageUploadService` (singleton)

**Trade-offs:** Vendor coupling mitigated by `IImageUploadService` abstraction (swap to Azure Blob or S3 by implementing interface).

**Impact:** All projects build cleanly (0 warnings, 0 errors); existing tests updated for new `CoverImageUrl` parameter.

---

### Event Endpoint DTO Pattern (2026-03-21)

**Author:** Tank (Backend Dev)

**Context:** Backend event creation endpoint returned raw `Event` domain entities with navigation properties, causing serialization cycles and type mismatches with frontend expectations (enums vs strings).

**Decision:** Added `EventResponse` DTO that decouples domain from API contracts. All endpoints now return DTOs:
- GET `/api/events` → `List<EventResponse>`
- GET `/api/events/{id}` → `EventResponse`
- POST `/api/events` → `EventResponse`
- PUT `/api/events/{id}` → `EventResponse`

**Benefits:**
- ✅ Type safety: Frontend `EventDto` matches Backend `EventResponse`
- ✅ Enum consistency: Serialized as strings via `JsonStringEnumConverter`
- ✅ No serialization issues: DTOs have no navigation properties
- ✅ API stability: Domain changes don't break frontend

**Validation added:** Title (required, max 200 chars), Capacity (1-10,000), EndDate >= StartDate, CreatedBy (required).

---

### Notification Pattern for Dialog Components (2026-03-21)

**Author:** Trinity

**Decision:** Implement reusable notification pattern for dialog components:
1. Dialog emits notifications via `[Parameter] EventCallback<string> OnNotification`
2. Parent page renders `<Notification>` with `@bind-IsVisible`
3. Parent handler determines `AlertVariant` based on message content

**Implementation:** Applied to CreateEventDialog, Home.razor, Events.razor

**Impact:**
- ✅ Consistent UX across create flows
- ✅ Testable via `data-test="notification"`
- ✅ Pattern reusable for other dialogs (edit, delete, etc.)

---

### Add Keycloak Container to Aspire AppHost (2026-03-21)

**Author:** Tank (Backend Dev)

**Context:** Auth system supports Entra/Auth0/Keycloak via config, but no local OIDC testing on `main` without external IdP.

**Decision:** Added `Aspire.Hosting.Keycloak` (preview 13.1.2-preview.1.26125.13) to AppHost. `dotnet run --project AppHost/AppHost` now spins up Keycloak on port 8080.

**Trade-offs:**

- Preview package (may need version bumps as Aspire 13.x stabilizes)
- Port 8080 reserved (adjustable in AppHost.cs)
- Realm setup is manual (bethuya realm + client creation in admin)

**Impact:** Auth docs updated in README; SECURITY.md refreshed; all tests pass.

---
