# Community OS v2 Design Proposal

**Date:** 2026-08-05  
**Owner:** Neo (Lead/System Architect)  
**Scope:** Issue #50 only. Design proposal, decision trail, and implementation sequencing. No production behavior change.

## Why this document exists

The approved Developer Testing Harness stack ends at Layer 3. There is no approved Layer 4 code slice for that stack. The next documented work is issue #50, which is explicitly a production-domain design exercise for Community OS v2, not a continuation of harness mechanics.

This proposal is grounded in the domain that already exists today:

- `CommunityMember` is the canonical member identity aggregate.
- `ExternalIdentity` is already the trusted cross-platform identity-link seam.
- `ParticipationLedgerEntry` is already the canonical cross-connector evidence feed, with member-scoped dedupe on `(CommunityMemberId, Connector, ProvenanceKey)`.
- `CommunityPassportService` and `CommunityJourneyReadModelService` are projections over existing member/profile/registration/ledger data, not new sources of truth.
- `MentorProfile` exists, but only for mentor opt-in/discoverability; there is no mentor-mentee relationship or session model yet.
- `Decision` + `RecommendationEnvelope` already provide the human-approval and audit foundation for recommendation drafts.
- Development-only endpoints already exist under `/api/dev`; they are environment-gated but not yet modeled as audited, dataset-scoped operations.

## Architectural guardrails

1. **Aspire-first, but not service-happy.** The first Community OS v2 stack should stay inside the existing backend + Postgres resource set. No new Aspire resource is justified until workload evidence proves that graph analytics, dataset reset orchestration, or recommendation refresh jobs cannot live behind the current `backend` boundary.
2. **Authorization stays separate from community progression.** Platform roles remain `Admin`, `Organizer`, `Curator`, and `Attendee`. Community progression states such as volunteer readiness, leadership candidacy, or mentor status must not become ASP.NET roles.
3. **Scoped capability beats global role sprawl.** New protected actions should use scoped capability grants, not a new global `CommunityLead` role.
4. **Passport and ledger are reused, not cloned.** New domains may reference `CommunityMember`, `ExternalIdentity`, and `ParticipationLedgerEntry`, but they must not duplicate member privacy settings, connector evidence, or recommendation audit payloads.
5. **Human approval remains mandatory for recommendations.** Member-level recommendations, mentorship pairing suggestions, volunteer suggestions, and community health actions continue to draft through `Decision` records and stay unapplied until a human approves them.
6. **Sensitive member-level insights stay server-side.** Any UI for member insights, volunteer history, mentorship sessions, or graph editing remains server-rendered in Web. If AI-assisted scoring or narrative is later added for member-level PII, the scoring boundary must use Foundry Local or deterministic local logic; it must not leak PII to cloud providers.
7. **Deletion must be explicit.** Every new v2 context must define how member erasure removes or anonymizes personal state while preserving non-identifying operational aggregates.
8. **Development tooling must be dataset-scoped.** No future quick-action or reset endpoint may operate by “best effort” table filtering or broad delete-by-slug logic.

## Capability decisions

### 1. Community Graph visualization, ownership, edge provenance, privacy, deletion, and network analytics

- **Production use case:** Give organizers a real community-topology view: who collaborates across events, projects, mentoring, and volunteering; which members are isolated; which members connect chapters; and where re-engagement or moderation work is needed. This is operational community intelligence, not fixture support.
- **Bounded context / aggregate ownership:** Introduce a new **Community Topology** context. `CommunityGraphEdge` is a new aggregate for **asserted** relationships only (for example, mentor-of, chapter-lead-of, project-maintainer-of, ownership-of). `CommunityGraphEdgeEvidence` stores provenance references to existing evidence (`ParticipationLedgerEntry`, `MentorshipRelationship`, `ProjectMembership`, `VolunteerAssignment`, or manual organizer assertion). **Derived** graph connections are projections; they are not authoritative writes.
- **Authorization vs. progression:** Creating, editing, or removing asserted edges is an authorization concern handled by organizer/admin/community-lead capability. Edge weights, centrality, “connector” labels, and community-health metrics are read models, not progression state on the member.
- **Privacy / residency / deletion / audit:** The graph must honor `CommunityMember.Visibility`, `ShareParticipationWithOrganizers`, and `IsDiscoverableToCommunity`. Non-discoverable members are omitted from public/discoverable graph views. Member erasure removes asserted edges owned by or targeting that member and triggers recomputation of derived edges. Manual edge assertions and removals are audited with actor, reason, trace id, and evidence references. No raw AIDE fields or protected traits belong in edge payloads.
- **Database / migration impact:** New tables: `CommunityGraphEdges`, `CommunityGraphEdgeEvidence`. Optional later cache table: `CommunityGraphMetrics` if query cost demands it. Indexes: `(ScopeType, ScopeId, SourceMemberId, EdgeKind)`, `(TargetNodeType, TargetNodeId)`, and `(EvidenceType, EvidenceId)` for provenance lookups. No duplication of passport or ledger payloads.
- **Aspire / resource impact:** Keep this inside the existing backend. Use query services and database projections first. A dedicated analytics worker is deferred until there is measured pressure on interactive requests.

### 2. Chapter administration, multi-chapter hierarchy, and community federation

- **Production use case:** HackerspaceMumbai will eventually need real organizational structure: sub-communities, city chapters, working groups, and federation with partner communities. This is administrative infrastructure, not seed scaffolding.
- **Bounded context / aggregate ownership:** Introduce a new **Community Administration** context. `Chapter` is a new aggregate with parent/child hierarchy. `ChapterMembership` records a member’s affiliation to a chapter. `FederationLink` is a separate aggregate for external partner-community trust relationships and synchronization contracts. `CommunityMember` stays the person identity record; it does not absorb chapter hierarchy behavior.
- **Authorization vs. progression:** Chapter membership is domain state. Managing a chapter, approving chapter-level configuration, and maintaining federation links are authorization capabilities. They must not be represented as member journey stages or platform roles.
- **Privacy / residency / deletion / audit:** Chapter membership is personal data. Member erasure must remove identifiable membership rows, while preserving aggregate headcounts via anonymized historical counts if needed. Federation links must default to metadata-only exchange; no member PII crosses chapter boundaries without an explicit connector contract and residency review. Hierarchy changes and membership-admin actions must be audited.
- **Database / migration impact:** New tables: `Chapters`, `ChapterMemberships`, `FederationLinks`. Migration sequencing: first create the root HackerspaceMumbai chapter, then add nullable `HomeChapterId` to `CommunityMembers`, backfill from existing `CommunitySlug`, keep `CommunitySlug` during transition, and only remove or demote it after consumers are moved. Add indexes on `(ParentChapterId, Slug)` and `(ChapterId, CommunityMemberId)`.
- **Aspire / resource impact:** No new resource. This belongs in the existing backend and database. If federation later needs scheduled connector jobs, those should still begin as backend-hosted jobs before any service split is approved.

### 3. Project communities and member-project participation

- **Production use case:** Community members do not just attend events; they join projects, build working groups, and contribute to shared initiatives over time. Bethuya needs first-class project communities so organizers can steward them beyond one-off events.
- **Bounded context / aggregate ownership:** Introduce a new **Project Communities** context. `ProjectCommunity` is a new aggregate for a project or working group. `ProjectMembership` records maintained membership and participation status. Activity evidence still belongs to `ParticipationLedgerEntry`; if project-scoped evidence is required, extend the ledger with a nullable `ProjectCommunityId` rather than creating a second activity ledger.
- **Authorization vs. progression:** Project maintainer or steward capability is authorization data. Project membership, active/inactive participation, and contribution history are domain state.
- **Privacy / residency / deletion / audit:** Project membership visibility must honor member discoverability settings and project visibility rules. Member erasure removes personal project memberships and project-scoped ledger attribution, then recomputes derived project insights. Maintainer changes and membership removals are audited. Public project views must avoid exposing organizer-only evidence or private member notes.
- **Database / migration impact:** New tables: `ProjectCommunities`, `ProjectMemberships`. Later nullable ledger extension: `ParticipationLedgerEntries.ProjectCommunityId` with index `(ProjectCommunityId, OccurredAt)` if project-scoped timeline queries are needed. Add unique index on `(ProjectCommunityId, CommunityMemberId)` for active memberships.
- **Aspire / resource impact:** Existing backend only. No AppHost change is justified for project communities alone.

### 4. Volunteer progression engine, assignments, hours, and lifecycle

- **Production use case:** Bethuya already produces volunteer suggestions in curation responses, but organizers still need a real operational model for assigning volunteers, tracking completion, understanding burnout risk, and recognizing long-term contributors.
- **Bounded context / aggregate ownership:** Introduce a new **Volunteer Coordination** context. The current curation opportunity engine remains **suggestion-only**. Add `VolunteerPlan` as a per-event or per-initiative aggregate containing persisted role and shift definitions, and `VolunteerAssignment` as the aggregate recording one member’s proposed/accepted/completed assignment. Volunteer readiness, burnout risk, and lifecycle stage are read models derived from assignments + ledger evidence, not stored as mutable role flags on `CommunityMember`.
- **Authorization vs. progression:** Creating plans and assigning volunteers are organizer/community-lead actions. Accepting, declining, completing, or pausing volunteer commitments are business-state transitions on assignments. Leadership or readiness status stays in read models, not auth roles.
- **Privacy / residency / deletion / audit:** Volunteer notes, attendance reliability, and completion history are personal operational data visible only to the member and authorized coordinators. Public recognition is opt-in. Erasure removes personal notes and direct assignment links while preserving anonymized aggregate service totals if policy requires them. Assignment creation, reassignment, completion overrides, and manual hour adjustments must be audited.
- **Database / migration impact:** New tables: `VolunteerPlans`, `VolunteerPlanShifts`, `VolunteerPlanRoles`, `VolunteerAssignments`. Consider `VolunteerAssignmentEvents` only if event sourcing is later needed; do not start there. Indexes: `(EventId, Status)`, `(CommunityMemberId, Status)`, `(VolunteerPlanId, ShiftKey, RoleKey)`. Avoid a separate “hours ledger” unless assignment + completion timestamps prove insufficient.
- **Aspire / resource impact:** Existing backend only. No new worker is needed in the first stack; assignment readiness projections can be computed in-process.

### 5. Advanced mentor/mentee relationships and session modeling

- **Production use case:** The current mentor directory solves discoverability only. Real community mentoring needs accepted pairings, goals, sessions, notes, and closure states.
- **Bounded context / aggregate ownership:** Extend the existing **Mentorship** context. Keep `MentorProfile` as the mentor opt-in aggregate. Add `MentorshipRelationship` as the pairing aggregate (mentor, mentee, scope, status, goals, created-by path). Add `MentorshipSession` as a separate aggregate keyed to a relationship, because session history can grow independently and needs its own retention policy.
- **Authorization vs. progression:** Opt-in/discoverability stays on `MentorProfile`. Pairing coordination and administrative access are authorization concerns. Relationship status (`Proposed`, `Active`, `Paused`, `Closed`) and session outcomes are domain state.
- **Privacy / residency / deletion / audit:** Session notes are sensitive and must remain server-side. If AI summaries are ever allowed for session notes, they must use Foundry Local because the content can contain PII and sensitive career/personal context. Erasure removes or anonymizes session notes and closes active relationships; aggregate counts may remain only in de-identified form. Relationship creation, acceptance, closure, and session-note visibility changes must be audited.
- **Database / migration impact:** New tables: `MentorshipRelationships`, `MentorshipGoals` (if goals are not embedded JSON), `MentorshipSessions`. Unique constraint on one active relationship per `(MentorMemberId, MenteeMemberId, Scope)` to prevent duplicates. Indexes on mentor, mentee, status, and session date.
- **Aspire / resource impact:** Existing backend only. No new service is justified until mentorship sessions require asynchronous reminders or calendar integration.

### 6. Community Lead capability and authorization policy

- **Production use case:** Chapter administration, project stewardship, graph curation, and volunteer coordination all create protected actions that are narrower than full platform-wide organizer authority. Delegation is therefore justified.
- **Bounded context / aggregate ownership:** Add `CommunityCapabilityGrant` to the **Community Administration** context. This aggregate grants scoped capabilities such as `ManageChapter`, `ManageProjectCommunity`, `ManageVolunteerPlan`, `ManageCommunityGraph`, or `ReviewMemberInsights` for a specific chapter or project scope.
- **Authorization vs. progression:** **Recommendation:** do **not** add `CommunityLead` to `BethuyaRoleNames`. A global ASP.NET role would conflate platform-wide authorization with community-scoped stewardship. Community leadership belongs in scoped capability grants, while member journey states (emerging contributor, leadership candidate, volunteer readiness) remain read models.
- **Privacy / residency / deletion / audit:** Capability grants are security-sensitive records. Grant/revoke actions must be immutable-audited with actor, scope, reason, and trace id. Member erasure should revoke active grants tied to that member; audit records may retain pseudonymized subject references for compliance.
- **Database / migration impact:** New table: `CommunityCapabilityGrants` with unique index on `(SubjectMemberId, Capability, ScopeType, ScopeId)`. No change to platform role constants in the first stack. Introduce new policy names only when the first scoped protected action lands.
- **Aspire / resource impact:** Existing backend only. Capability resolution belongs in the auth policy layer, not a new service.

### 7. Audited development quick-action APIs

- **Production use case:** Internal support, demo rehearsal, onboarding, and incident reproduction in non-production environments need fast operator commands. That is operational product value, not “test-data only” value.
- **Bounded context / aggregate ownership:** Introduce a new **Development Harness Operations** context for command surfaces. Quick actions do not own synthetic data directly; they operate against `SimulationDataset` aggregates defined in the next section and record their execution in `DevelopmentActionRun`.
- **Authorization vs. progression:** These are not community progression features. They are development/operations capabilities. The command surface must require all of the following: Development environment, `Authentication:Provider=None`, and an explicit dev capability grant such as `dev.quickactions`. Do not copy the unauthenticated `ApprovalEndpoints` pattern into this area.
- **Privacy / residency / deletion / audit:** Every quick action must require a human-supplied reason, emit immutable audit metadata (actor, selected dev persona, dataset id, action key, affected entity count, trace id, timestamp), and support dry-run preview where deletion or reassignment is involved. Quick actions must never target rows that are not owned by a simulation dataset. If a query would touch unowned rows, the action must fail closed.
- **Database / migration impact:** New tables: `DevelopmentActionRuns` and `DevelopmentActionRunArtifacts` if affected-entity details are required. No direct “dev-only” columns should be added to production aggregates just to support these commands.
- **Aspire / resource impact:** Keep the endpoints in the existing backend and map them only in Development. No new AppHost resource. If a long-running action later appears, it can be queued behind the same backend first.

### 8. Simulation dataset ownership and safe, scoped reset workflows

- **Production use case:** Non-production environments need reliable synthetic datasets for demos, training, smoke verification, and partner rehearsals without touching real member data.
- **Bounded context / aggregate ownership:** In **Development Harness Operations**, introduce `SimulationDataset` as the owning aggregate and `SimulationArtifact` as the artifact registry that records each created entity reference `(EntityType, EntityId)` for that dataset. This is the critical safety boundary: reset operates on the artifact registry, not by broad table heuristics or by guessing which rows “look seeded.”
- **Authorization vs. progression:** Dataset ownership and reset permissions are operational capabilities only. They must not be expressed as organizer/community journey state.
- **Privacy / residency / deletion / audit:** Phase 1 must be **synthetic-only**. Do not support cloning real member data into simulation datasets. Dataset reset may hard-delete synthetic artifacts, but the `SimulationDataset` metadata and reset audit trail remain. Every reset must be scoped to a specific dataset id, support preview mode, and record who triggered it and why.
- **Database / migration impact:** New tables: `SimulationDatasets`, `SimulationArtifacts`, `SimulationResetRuns`. This avoids contaminating `Event`, `Registration`, `AttendeeProfile`, `CommunityMember`, `Decision`, or ledger tables with fixture-only columns. Indexes: `(DatasetId, EntityType, EntityId)` and `(OwnerSubject, CreatedAt)`.
- **Aspire / resource impact:** Existing backend and database only. There is no architectural need for a separate reset service in the first stack.

### 9. Member-level recommendation/read models for retention risk, re-engagement, technology affinity, emerging contributors, volunteer readiness, and leadership candidates

- **Production use case:** Organizers need a trustworthy, explainable way to spot disengagement risk, discover members who are ready for volunteering or leadership, and identify which project or technology tracks are sticky. This is core community stewardship.
- **Bounded context / aggregate ownership:** Extend the existing **Community Insights / Recommendations** read-model boundary rather than creating a new authoritative write domain. Add a dedicated `CommunityInsightReadModelService` instead of bloating `CommunityJourneyReadModelService`. Drafted organizer actions continue to use `RecommendationEnvelope` + `Decision`; no new recommendation aggregate is needed.
- **Authorization vs. progression:** Viewing scoped member insights is an authorization concern for organizers/admins/community leads. The underlying scores, classifications, and readiness labels are read models derived from existing evidence; they are not permanent status fields on the member.
- **Privacy / residency / deletion / audit:** Member-level insights are sensitive. They must stay server-side and be limited to authorized reviewers. `IsDiscoverableToCommunity` and organizer-sharing flags must gate which members can be included in community-facing suggestion flows. Erasure removes cached member-level insights and recomputes aggregates. If AI narrative is later layered on top of the scores, that narrative must run through Foundry Local because it is describing individual members.
- **Database / migration impact:** Phase 1 should be compute-on-read plus targeted indexes on existing evidence tables. Do **not** start with a persistent ML-style score table. A later cache table such as `CommunityInsightSnapshots` is acceptable only after query cost is measured and only for recomputable outputs. Sequencing matters: retention/re-engagement can land on current registration + ledger data; technology affinity and emerging-contributor models should wait for project-community evidence; volunteer readiness should wait for volunteer assignments; leadership-candidate read models should reuse those same signals.
- **Aspire / resource impact:** Existing backend only. No new resource until actual refresh latency proves a need for background snapshotting.

## Cross-cutting migration and implementation rules

1. **Do not mutate `CommunityPassportService` into a write-heavy god service.** Passport remains the member-facing projection and privacy surface.
2. **Do not create a second evidence ledger.** New domains reference `ParticipationLedgerEntry` ids or enrich the existing ledger with scoped foreign keys where necessary.
3. **Prefer nullable additive migrations with backfills.** `CommunitySlug` must not disappear before chapter ids exist and all consumers are moved.
4. **Use read models for progression.** Emerging contributor, volunteer readiness, connector score, leadership candidate, and re-engagement risk remain derived outputs until there is a proven need for persisted snapshots.
5. **Keep recommendation audit uniform.** New member-level recommendation flows must reuse `Decision` + `RecommendationEnvelope` rather than inventing a parallel approval store.

## Proposed implementation PR stack

1. **v2-PR1 — Community Administration foundation**
   - Add `Chapter`, `ChapterMembership`, `FederationLink`, and `CommunityCapabilityGrant`.
   - Backfill root chapter from existing `CommunitySlug`.
   - Dependency: none; this is the authorization and tenancy foundation.
2. **v2-PR2 — Project Communities**
   - Add `ProjectCommunity` and `ProjectMembership`.
   - Add scoped maintainer capability checks.
   - Dependency: v2-PR1 for chapter ownership and scoped capability grants.
3. **v2-PR3 — Community Graph**
   - Add asserted `CommunityGraphEdge` + provenance model.
   - Add graph projections that reuse chapter/project/membership/mentorship/ledger evidence.
   - Dependency: v2-PR1 and v2-PR2.
4. **v2-PR4 — Volunteer Coordination**
   - Promote current opportunity-engine outputs into persisted `VolunteerPlan` and `VolunteerAssignment`.
   - Add lifecycle and hours read models.
   - Dependency: v2-PR1 for scoped capabilities; can reuse existing event and curation data immediately.
5. **v2-PR5 — Mentorship Relationships and Sessions**
   - Extend `MentorProfile` with `MentorshipRelationship` and `MentorshipSession`.
   - Keep human approval and audit on pairing recommendations.
   - Dependency: v2-PR1 for scoped coordination capabilities.
6. **v2-PR6 — Community Insight Read Models**
   - Add `CommunityInsightReadModelService`.
   - Implement retention risk, re-engagement, leadership candidate, and volunteer readiness first.
   - Defer technology affinity/emerging-contributor outputs until project evidence from v2-PR2 exists.
   - Dependency: v2-PR2, v2-PR4, and v2-PR5 for full signal coverage.
7. **v2-PR7 — Development Harness Operations**
   - Add `SimulationDataset`, `SimulationArtifact`, `SimulationResetRun`, and `DevelopmentActionRun`.
   - Replace ad hoc development mutators with audited, dataset-scoped quick actions.
   - Dependency: independent of member/community domains, but should reuse the capability-grant discipline from v2-PR1.

## Final recommendations

- **Approve issue #50 as a design-only outcome.** It should not produce code in this branch.
- **Do not add a global `CommunityLead` platform role.** Add scoped capability grants instead, when the first protected chapter/project action lands.
- **Keep Community OS v2 inside the existing backend/AppHost topology for the first implementation stack.** No new Aspire resource is justified yet.
- **Treat `SimulationDataset` + `SimulationArtifact` as the hard safety boundary for every future dev quick action or reset flow.**
- **Keep member-level insights explainable and human-reviewed.** Deterministic read models first; PII-aware AI narration only behind Foundry Local if it is ever introduced.
