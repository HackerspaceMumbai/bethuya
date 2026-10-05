# Homepage v3: Community Operations Command Center

## Purpose

Homepage v3 is Bethuya's claims-aware homepage. It resolves the signed-in person into one of two surfaces — a **Community Operations Command Center** for operating roles, and a **Community Participation** journey home for participants — so that each person understands their situation within 30 seconds.

For operators, that means:

1. **What changed?**
2. **What needs attention?**
3. **What should I do next?**

For participants, it means:

1. **Where am I in my journey?**
2. **What can I do next?**
3. **How can I participate more deeply?**

The homepage is not a smaller copy of every Bethuya workspace. It synthesizes community signals, prioritizes human attention, and routes organizers into the dedicated workspace where deeper analysis or action belongs.

This design follows Bethuya's core ethos:

> AI recommends. Humans decide. Communities thrive.

Recommendations remain explainable and reviewable. The command center never converts an intelligence signal directly into an irreversible community decision.

## Two homepage surfaces

Homepage v3 resolves one of two surfaces from the authenticated principal. There is no surface picker and no "View As" control.

| Surface | Audiences | Validation questions |
| --- | --- | --- |
| **Community Operations** | Community Administrator, Event Organizer, Volunteer Lead, Mentorship Lead | What changed? What needs attention? What should I do next? |
| **Community Participation** | Event Participant, Emerging Contributor, Community Member | Where am I in my journey? What can I do next? How can I participate more deeply? |

**One command center, role-aware content.** Every operating role sees the *same* module layout and the same component hierarchy. Only module *content* adapts: a volunteer lead leads with coverage gaps, a mentorship lead with mentor supply, an organizer with capacity risk, an administrator with retention risk. Bethuya deliberately avoids four divergent operator homepages, because divergence fragments the operating model and multiplies the surface area that future intelligence providers must satisfy.

**Participation is not a reduced command center.** The participation surface never renders a community snapshot, capacity planning, volunteer coverage metrics, waitlist management, an attention queue, review queues, or operational deadlines. Operational pressure is not a participant's burden. The participation surface instead renders a community passport, onboarding or journey timeline, upcoming activities, contribution history, earned sections, and recommended opportunities.

### Why split the surfaces

Operators and participants ask structurally different questions. Showing an attention queue to an attendee creates anxiety without agency, and showing a journey timeline to an organizer buries operational risk. A single page that tries to serve both degrades into conditional clutter. Splitting at the surface boundary — while keeping a shared resolver, shared models, and shared provider seams — keeps each experience honest and each contract replaceable.

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
- People To Watch

It emphasizes:

- participation and retention;
- mentorship capacity;
- volunteer and leadership development;
- re-engagement;
- emerging contributors;
- community health over time.

Attention items, review work, and touchpoints remain visible, but they support the longer-term community perspective. The primary-work order is Insight → People To Watch → Attention Queue → Human Review Queue, and the mode question reads "How is the community evolving?"

### Event Mode

Event Mode reprioritizes the command center around near-term execution.

The leading experience is:

- Community Attention Queue;
- Pending Approvals;
- expanded Upcoming Touchpoints readiness.

Generic community snapshot metrics are replaced by event-operational metrics:

- event readiness;
- confirmed capacity;
- waitlist pressure;
- volunteer coverage.

Upcoming Touchpoints expands to expose readiness, capacity, waitlist, volunteer coverage, and risk indicators. Community Insight becomes an event intelligence brief, and People To Watch prioritizes people who can directly improve event outcomes. In the rail, Upcoming Deadlines is promoted above approvals because approvals have already moved into the main column. The mode question reads "Can this event succeed?"

The transition must be unmistakable: hierarchy, metric vocabulary, module emphasis, and the mode question all change. A mode indicator alone is not a mode.

### Recommendation and override behavior

The deterministic provider recommends Event Mode when Hacktoberfest is within 21 days; otherwise it recommends Strategic Mode. The clock is injected through `TimeProvider`, which keeps the rule testable.

The organizer may select either mode manually. `RecommendedMode` records the rule-driven recommendation, while `EffectiveMode` records what the UI is currently rendering. `ModeReason` explains whether the mode was recommended or manually selected.

Critical attention is never hidden by a manual override.

## Command-center modules

Operations information architecture, in render order:

Header → Community Snapshot → Community Insight → People To Watch → Community Attention Queue → Human Review Queue → Upcoming Touchpoints → Rail (Pending Approvals, Upcoming Deadlines, Quick Actions).

| Module | Question answered | Command-center responsibility |
| --- | --- | --- |
| Community Snapshot | What changed at a glance? | Show a small set of mode-specific health or readiness measures. |
| Community Insight | What pattern matters this week? | Explain the most useful strategic or event-execution pattern and its source signals. |
| People To Watch | Who is moving, and why does it matter? | Surface journey transitions with an explicit watch reason. Awareness only — never a task, ranking, or approval. |
| Community Attention Queue | What needs intervention? | Prioritize explainable risks and opportunities, then route to the owning workspace. |
| Human Review Queue | What requires a decision? | Make pending human approvals visible and actionable. |
| Upcoming Touchpoints | What is approaching? | Cover events, mentorship sessions, volunteer orientations, and working groups — compact in Strategic Mode, expanded to readiness, capacity, waitlist, volunteer coverage, and risk in Event Mode. |
| Pending Approvals | What is blocking progress? | Move event-critical approvals higher in Event Mode. |
| Upcoming Deadlines | What is time-sensitive? | Surface the next meaningful deadlines without reproducing a full calendar. |
| Quick Actions | What can I start right now? | Offer a small set of role-appropriate entry points into the owning workspace. |
| Workspace navigation | Where do I investigate or act? | Route to the dedicated Bethuya workspace. |

### Why Upcoming Touchpoints, not Upcoming Events

Communities are continuous; events are punctuation. Naming the module "Events" quietly told organizers that mentorship sessions, volunteer orientations, and working groups were second-class. Touchpoints restores the journey framing while keeping event readiness as the richest case.

### Why People To Watch is awareness-only

Converting momentum into a task invites mechanical promotion. The module names the journey transition and the reason it is worth noticing, then stops. A human decides whether, when, and how to act.

## Participation modules

| Module | Question answered | Responsibility |
| --- | --- | --- |
| Community Passport | Where am I? | Name the journey stage and show progress toward the next one. |
| Getting Started / Journey Timeline | What have I done, and what is next? | Onboarding steps for new participants; a contribution timeline for established ones. |
| Upcoming Activities | What is coming up for me? | Show only the participant's own touchpoints. |
| Earned Sections | What have I unlocked? | Reveal volunteering, mentorship, working groups, and recognition as participation deepens. |
| Recommended Opportunities | How can I participate more deeply? | Offer one clear, human-reviewable next step. |

## Architecture

The homepage depends on:

```text
Home.razor
    |
    +-- authenticated ClaimsPrincipal
    |       |
    |       v
    |   ICommandCenterAudienceResolver  --> CommandCenterAudience (Role + Surface)
    |
    +-- Surface == Operations ------> ICommunityCommandCenterService
    |                                       |
    |                                       v
    |                                 CommunityCommandCenter
    |                                       +-- ModeQuestion
    |                                       +-- Snapshot
    |                                       +-- Insight
    |                                       +-- PeopleToWatch
    |                                       +-- AttentionItems
    |                                       +-- Reviews
    |                                       +-- Touchpoints
    |                                       +-- Approvals / Deadlines
    |                                       +-- QuickActions
    |                                       +-- Workspaces
    |
    +-- Surface == Participation ---> ICommunityParticipationService
                                            |
                                            v
                                      CommunityParticipationHome
                                            +-- Passport
                                            +-- OnboardingSteps / Timeline
                                            +-- Activities
                                            +-- EarnedSections
                                            +-- Opportunities
```

The current implementation is:

- `ICommandCenterAudienceResolver` — claims-to-display-audience boundary that never grants authorization, and that also selects the surface;
- `ClaimsCommandCenterAudienceResolver` — deterministic development-persona mapping with safe production role fallback;
- `ICommunityCommandCenterService` — stable operations provider boundary;
- `DeterministicCommunityCommandCenterService` — rule-based v3 operations provider;
- `ICommunityParticipationService` — stable participation provider boundary;
- `DeterministicCommunityParticipationService` — rule-based v3 participation provider;
- `CommunityCommandCenter`, `CommunityParticipationHome` and their child records — immutable, display-ready projections;
- `Home.razor`, command-center components, and `ParticipationSurface` — surface- and mode-aware presentation hierarchy.

Two provider boundaries rather than one keeps the seams honest: a future Community Intelligence service can replace the operations provider without touching participation, and a future member-journey service can replace participation without destabilizing operations.

The provider returns one cohesive projection for a selected role and optional mode override. Components do not calculate intelligence or fetch subsystem-specific data independently.

### Why a display-ready projection?

A single projection keeps prioritization coherent. Snapshot measures, insight, momentum, attention, reviews, events, and deadlines are selected for the same role and mode instead of arriving as unrelated widgets with conflicting priorities.

It also prevents the UI from becoming coupled to future graph schemas, scoring algorithms, agent protocols, or service-specific transport contracts.

### Claims-driven audience resolution

The homepage does not contain a “View As” control. It reads the authenticated `ClaimsPrincipal` and resolves a display-only audience before requesting the projection. This prevents client UI state, query strings, or form values from selecting an operational perspective.

Development personas are recognized only through the allowlisted `bethuya:development-persona` claim emitted by local authentication with the `Bethuya.Development` issuer. A production identity with a colliding subject therefore cannot acquire a development persona experience. Unknown production identities fall back conservatively: Admin becomes Community Administrator, Organizer becomes Event Organizer, and every other identity receives the Community Member experience.

Audience is deliberately separate from authorization. It influences deterministic narrative, ordering, and navigation but never creates role claims, satisfies a policy, or grants access to a protected endpoint. Live providers must continue to enforce authorization independently. If no authenticated principal is available, Home renders only a sign-in-required state and does not request or expose the command-center projection; this keeps the shared MAUI route safe even before its authentication bridge supplies a Blazor authentication cascade.

### Six persona user stories

| Persona | Surface · audience | User story |
| --- | --- | --- |
| Anish | Participation · Event participant | As a newer event participant, I want one welcoming next step matched to my participation rhythm so I can contribute without needing an existing leadership role. |
| Priya | Operations · Volunteer Lead | As a volunteer lead, I want coverage gaps and emerging volunteer owners prioritized so I can close event needs while growing community leadership. |
| Rohan | Operations · Event Organizer | As an event organizer, I want readiness, approvals, capacity, and execution risks surfaced first so I can keep the event moving. |
| Maya | Operations · Mentorship Lead | As a mentorship lead, I want mentor supply, delayed pairings, and promising mentee journeys highlighted so I can make thoughtful matches. |
| Farah | Participation · Emerging contributor | As a member whose session contribution is gaining momentum, I want earned sections and the next human-reviewed opportunity made visible so I can grow into facilitation and leadership. |
| Vikram | Operations · Community Administrator | As a community administrator, I want community health and cross-workspace decisions synthesized so I can balance long-term growth with operational risk. |

The four operating personas share one command center and one component hierarchy; only module content differs. The two participation personas share one journey surface, differentiated by onboarding versus journey mode and by which sections have been earned. Strategic/Event mode remains available within every operating perspective, and community-critical warnings remain visible.

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

Live services should be integrated incrementally behind `ICommunityCommandCenterService` and `ICommunityParticipationService`.

Expected replacements include:

| Current deterministic area | Future source |
| --- | --- |
| Community Snapshot and Insight | Community Intelligence service |
| People To Watch and relationship signals | Community Graph |
| Opportunities and contributor next steps | Opportunity Engine |
| Touchpoint readiness and approvals | Event Operations services |
| Participation passport, timeline, and earned sections | Member journey services |
| Cross-workspace prioritization | Explainable orchestration rules or agents |

Integration rules:

1. Preserve the `CommunityCommandCenter` and `CommunityParticipationHome` projections until a demonstrated product need requires contract evolution.
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
- Playwright coverage for explicit mode selection, hierarchy changes, surface resolution per persona, navigation, responsive layout, and visual evidence;
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
