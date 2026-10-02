# Homepage v3: Community Operations Command Center

## Purpose

Homepage v3 is Bethuya's claims-aware **Community Operations Command Center**. It is the place where a community member or organizer should understand, within 30 seconds:

1. **What changed?**
2. **What needs attention?**
3. **What should I do next?**

The homepage is not a smaller copy of every Bethuya workspace. It synthesizes community signals, prioritizes human attention, and routes organizers into the dedicated workspace where deeper analysis or action belongs.

This design follows Bethuya's core ethos:

> AI recommends. Humans decide. Communities thrive.

Recommendations remain explainable and reviewable. The command center never converts an intelligence signal directly into an irreversible community decision.

## Product principles

### Communities are journeys; events are moments

The default homepage perspective is community development over time: participation, retention, mentorship, volunteering, leadership readiness, and emerging contributors. Events matter because they create opportunities in that journey, not because event administration is the platform's sole purpose.

### Attention before administration

The homepage surfaces changes, risks, opportunities, approvals, and deadlines. Detailed editing, analysis, and operational workflows remain in Events, Volunteers, Mentorship, Community Health, Community Graph, Opportunity Engine, and Intelligence.

### Human review is a first-class workflow

Waitlist adjustments, volunteer assignments, mentor pairings, outreach drafts, accessibility requests, and similar recommendations appear as review work. The homepage may prioritize them, but a human owns the decision.

### Explainable prioritization

The interface states the active mode and why it was recommended. Organizers can override the recommendation without losing critical warnings.

### Intelligence-ready, not intelligence-blocked

Homepage v3 deliberately uses deterministic data and rule-driven logic. This validates the information architecture and operating model before Bethuya invests in live Community Intelligence, Community Graph analytics, Opportunity Engine recommendations, or agent orchestration.

Deterministic does not mean disposable. The UI consumes a stable projection contract so future providers can replace the data source without redesigning the page or its component hierarchy.

## Modes

Modes are operational lenses, not themes. Changing mode changes hierarchy, content, metrics, and visual emphasis.

### Strategic Mode

Strategic Mode keeps long-term community development primary.

The leading experience is:

- Community Insight of the Week
- People Gaining Momentum

It emphasizes:

- participation and retention;
- mentorship capacity;
- volunteer and leadership development;
- re-engagement;
- emerging contributors;
- community health over time.

Attention items, review work, and events remain visible, but they support the longer-term community perspective.

### Event Mode

Event Mode reprioritizes the command center around near-term execution.

The leading experience is:

- Community Attention Queue;
- Pending Approvals;
- expanded Upcoming Events readiness.

Generic community snapshot metrics are replaced by event-operational metrics:

- event readiness;
- confirmed capacity;
- waitlist pressure;
- volunteer coverage.

Upcoming Events expands to expose readiness, capacity, waitlist, volunteer coverage, and risk indicators. Community Insight becomes an event intelligence brief, and People Gaining Momentum prioritizes people who can directly improve event outcomes.

### Recommendation and override behavior

The deterministic provider recommends Event Mode when Hacktoberfest is within 21 days; otherwise it recommends Strategic Mode. The clock is injected through `TimeProvider`, which keeps the rule testable.

The organizer may select either mode manually. `RecommendedMode` records the rule-driven recommendation, while `EffectiveMode` records what the UI is currently rendering. `ModeReason` explains whether the mode was recommended or manually selected.

Critical attention is never hidden by a manual override.

## Command-center modules

| Module | Question answered | Command-center responsibility |
| --- | --- | --- |
| Community Snapshot | What changed at a glance? | Show a small set of mode-specific health or readiness measures. |
| Community Insight | What pattern matters this week? | Explain the most useful strategic or event-execution pattern and its source signals. |
| People Gaining Momentum | Who may be ready for an opportunity? | Surface journey changes without turning them into rankings or automatic promotions. |
| Community Attention Queue | What needs intervention? | Prioritize explainable risks and opportunities, then route to the owning workspace. |
| Human Review Queue | What requires a decision? | Make pending human approvals visible and actionable. |
| Upcoming Events | What is approaching? | Provide compact strategic context or expanded operational readiness, depending on mode. |
| Pending Approvals | What is blocking progress? | Move event-critical approvals higher in Event Mode. |
| Upcoming Deadlines | What is time-sensitive? | Surface the next meaningful deadlines without reproducing a full calendar. |
| Workspace navigation | Where do I investigate or act? | Route to the dedicated Bethuya workspace. |

## Architecture

The homepage depends on:

```text
Home.razor
    |
    +-- authenticated ClaimsPrincipal
    |       |
    |       v
    |   ICommandCenterAudienceResolver
    |
    v
ICommunityCommandCenterService
    |
    v
CommunityCommandCenter
    |
    +-- Snapshot
    +-- Insight
    +-- Momentum
    +-- AttentionItems
    +-- Reviews
    +-- UpcomingEvents
    +-- Deadlines
    +-- Workspaces
```

The current implementation is:

- `ICommandCenterAudienceResolver` — claims-to-display-audience boundary that never grants authorization;
- `ClaimsCommandCenterAudienceResolver` — deterministic development-persona mapping with safe production role fallback;
- `ICommunityCommandCenterService` — stable provider boundary;
- `DeterministicCommunityCommandCenterService` — rule-based v3 provider;
- `CommunityCommandCenter` and its child records — immutable, display-ready projection;
- `Home.razor` and command-center components — mode-aware presentation hierarchy.

The provider returns one cohesive projection for a selected organizer role and optional mode override. Components do not calculate intelligence or fetch subsystem-specific data independently.

### Why a display-ready projection?

A single projection keeps prioritization coherent. Snapshot measures, insight, momentum, attention, reviews, events, and deadlines are selected for the same role and mode instead of arriving as unrelated widgets with conflicting priorities.

It also prevents the UI from becoming coupled to future graph schemas, scoring algorithms, agent protocols, or service-specific transport contracts.

### Claims-driven audience resolution

The homepage does not contain a “View As” control. It reads the authenticated `ClaimsPrincipal` and resolves a display-only audience before requesting the projection. This prevents client UI state, query strings, or form values from selecting an operational perspective.

Development personas are recognized only through the allowlisted `bethuya:development-persona` claim emitted by local authentication with the `Bethuya.Development` issuer. A production identity with a colliding subject therefore cannot acquire a development persona experience. Unknown production identities fall back conservatively: Admin becomes Community Administrator, Organizer becomes Event Organizer, and every other identity receives the Community Member experience.

Audience is deliberately separate from authorization. It influences deterministic narrative, ordering, and navigation but never creates role claims, satisfies a policy, or grants access to a protected endpoint. Live providers must continue to enforce authorization independently. If no authenticated principal is available, Home renders only a sign-in-required state and does not request or expose the command-center projection; this keeps the shared MAUI route safe even before its authentication bridge supplies a Blazor authentication cascade.

### Six persona user stories

| Persona | Homepage audience | User story |
| --- | --- | --- |
| Anish | Member journey | As a newer community member, I want one welcoming next step matched to my participation rhythm so I can contribute without needing an existing leadership role. |
| Priya | Volunteer Lead | As a volunteer lead, I want coverage gaps and emerging volunteer owners prioritized so I can close event needs while growing community leadership. |
| Rohan | Event Organizer | As an event organizer, I want readiness, approvals, capacity, and execution risks surfaced first so I can keep the event moving. |
| Maya | Mentorship Lead | As a mentorship lead, I want mentor supply, delayed pairings, and promising mentee journeys highlighted so I can make thoughtful matches. |
| Farah | Community Member · Emerging Contributor | As a member whose session contribution is gaining momentum, I want the next human-reviewed opportunity made visible so I can grow into facilitation and leadership. |
| Vikram | Community Administrator | As a community administrator, I want community health and cross-workspace decisions synthesized so I can balance long-term growth with operational risk. |

These are six perspectives over one component hierarchy, not six separate homepages. Strategic/Event mode remains available within every perspective, and community-critical warnings remain visible.

## Deterministic provider rationale

The deterministic provider exists to answer product questions before infrastructure questions:

- Does the hierarchy help organizers orient quickly?
- Are Strategic and Event modes meaningfully different?
- Are recommendations understandable?
- Does each card route to the correct workspace?
- Is the human-review boundary clear?
- Which intelligence contracts are actually needed?

Building graph analytics or recommendation services first would encode assumptions before the workflow is validated. The deterministic slice lets Bethuya test the command-center experience with stable, repeatable scenarios.

The current data is intentionally scenario-based and rule-driven. It must not be represented as live measurement, production scoring, or inferred truth about real members.

## Future provider replacement

Live services should be integrated incrementally behind `ICommunityCommandCenterService`.

Expected replacements include:

| Current deterministic area | Future source |
| --- | --- |
| Community Snapshot and Insight | Community Intelligence service |
| People Gaining Momentum and relationship signals | Community Graph |
| Opportunities and contributor next steps | Opportunity Engine |
| Event readiness and approvals | Event Operations services |
| Cross-workspace prioritization | Explainable orchestration rules or agents |

Integration rules:

1. Preserve the `CommunityCommandCenter` projection until a demonstrated product need requires contract evolution.
2. Keep provider-specific DTOs and scoring details behind the service boundary.
3. Preserve `RecommendedMode`, `EffectiveMode`, and human-readable reasoning.
4. Never turn a recommendation into automatic acceptance, rejection, promotion, outreach, or publication.
5. Route PII-sensitive intelligence through approved local providers.
6. Replace modules incrementally; a mixed deterministic/live provider is acceptable during migration.
7. Keep failure explicit. The homepage shows an unavailable state rather than silently presenting stale or fabricated success data.

## Workspace boundaries

The command center routes to:

- Community Graph;
- Opportunity Engine;
- Events;
- Community Health;
- Volunteers;
- Mentorship;
- Intelligence.

Those workspaces own exploration, editing, workflow completion, and detailed records. Home owns synthesis, attention, and the next-action decision.

Before adding a homepage capability, ask:

> Does this help an organizer understand a change, prioritize attention, or choose the next action?

If not, it belongs in a dedicated workspace.

## Migration reliability decision

Homepage v3 exposed an existing startup failure caused by EF migration-model drift and an error-producing fresh-database migration-history probe.

The repaired migration is generated with complete EF metadata and a synchronized model snapshot. Before applying migrations, `MigrationHistoryBootstrapper` calls the active provider's `IHistoryRepository.CreateIfNotExistsAsync`.

Using the provider operation instead of executing a generated SQL script directly is intentional:

- the provider owns SQL generation;
- the provider owns concurrent-creation behavior;
- fresh databases do not produce a misleading missing-history-table command error;
- genuine migration failures remain visible.

This bootstrap is infrastructure reliability work, not homepage business logic, but it is part of the production path required to validate the homepage through the real Aspire topology.

## Verification contract

Homepage changes should preserve:

- TUnit coverage for deterministic rules, fixed-clock mode recommendation, role prioritization, and projection contents;
- bUnit coverage for Blazor Blueprint parameter safety and mode-specific render order;
- Playwright coverage for explicit mode selection, hierarchy changes, navigation, responsive layout, and visual evidence;
- fresh Aspire validation showing `migration-service` completes and Backend/Web become healthy;
- stable `data-test` selectors on native HTML wrappers;
- accessible mode controls, reasoning, loading, and error states.

Browser tests that assert Event Mode content must select Event Mode explicitly. Automatic recommendation belongs in fixed-clock provider tests so E2E behavior does not depend on the calendar date.

## Non-goals for v3

Homepage v3 does not attempt to deliver:

- live graph analytics;
- production recommendation scoring;
- autonomous agent orchestration;
- automatic attendee acceptance or rejection;
- automatic volunteer promotion;
- inferred sensitive traits;
- a replacement for dedicated operational workspaces;
- a general-purpose dashboard builder.

Those capabilities may evolve later, but they must preserve Bethuya's human-in-the-loop and community-development principles.
