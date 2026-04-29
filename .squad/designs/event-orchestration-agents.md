# Multi-Agent Event Orchestration Architecture

**Author:** Neo (Lead Architect)  
**Date:** 2026-04-28  
**Status:** Design Proposal  
**Version:** 1.0

---

## Executive Summary

This document defines the multi-agent architecture for Bethuya's event lifecycle management across three domains: **Event Planning**, **Attendee Curation**, and **Post-Meetup Reporting**. The design uses Microsoft Agent Framework (MAF) as the runtime, leverages Microsoft Foundry Hosted Agents for stateful persistence, enforces strict PII isolation via Foundry Local, and integrates cleanly with Aspire orchestration, existing copilot skills, and MCP service boundaries.

**Core Principle:** AI *drafts*, humans *approve*, community *owns*.

---

## 1. Agent Roles & Responsibilities

We define **7 agents** covering the three event lifecycle domains:

```
┌────────────────────────────────────────────────────────────────────┐
│                        EVENT LIFECYCLE                             │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  ┌───────────────┐   ┌───────────────┐   ┌───────────────┐       │
│  │   PLANNING    │   │   CURATION    │   │   REPORTING   │       │
│  └───────────────┘   └───────────────┘   └───────────────┘       │
│         │                    │                    │               │
│    ┌────┴────┐         ┌─────┴─────┐        ┌───┴───┐           │
│    │ Planner │         │  Curator  │        │Reporter│           │
│    │  Scout  │         │  Auditor  │        │        │           │
│    └─────────┘         └───────────┘        └────────┘           │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │         CROSS-CUTTING (ALL DOMAINS)                      │    │
│  │  ┌──────────┐     ┌──────────┐                          │    │
│  │  │Orchestrator│   │  Approver │                          │    │
│  │  └──────────┘     └──────────┘                          │    │
│  └──────────────────────────────────────────────────────────┘    │
└────────────────────────────────────────────────────────────────────┘
```

### 1.1 Planner Agent (Lead)

**Domain:** Event Planning  
**Type:** Lead Agent  
**Purpose:** Draft event agendas, session timings, and speaker suggestions based on historical patterns, venue constraints, and community interests.

**Responsibilities:**
- Analyze past event data (attendance, session ratings, speaker performance)
- Generate draft agendas with session timings and transitions
- Suggest speaker invitations with rationale
- Flag constraint violations (AV needs, timing conflicts, DEI gaps)
- Produce `AgendaDraft` for human approval

**Dependencies:**
- Scout Agent (venue and speaker availability data)
- Orchestrator (workflow coordination)
- Event history database (via MCP)

**Interactions:**
- Receives planning requests from Orchestrator
- Queries Scout for external availability data
- Emits `AgendaDraft` to Approver for human review

---

### 1.2 Scout Agent (Reviewer)

**Domain:** Event Planning  
**Type:** Reviewer Agent  
**Purpose:** Gather external context needed for planning: speaker availability, venue details, community interests, and competing events.

**Responsibilities:**
- Query speaker availability calendars (read-only APIs)
- Fetch venue capacity and equipment data
- Scrape community interest signals (Meetup.com, Discord activity)
- Check for competing events in the region/date range
- Surface conflict warnings to Planner

**Dependencies:**
- External APIs (calendar services, venue databases, social platforms)
- MCP tool boundaries for HTTP/GraphQL calls

**Interactions:**
- Invoked by Planner for context gathering
- Returns structured data (`SpeakerAvailability`, `VenueConstraints`, `CommunitySignals`)

---

### 1.3 Curator Agent (Lead) ⚠️ PII-Sensitive

**Domain:** Attendee Curation  
**Type:** Lead Agent  
**Purpose:** Assist humans to select attendees fairly when registrations exceed capacity (3× oversubscription), using FairnessBudget logic and consented DEI fields.

**Responsibilities:**
- Score registrant theme alignment (on-device ML scoring)
- Apply FairnessBudget nudges (diversity, first-time attendees, continuity)
- Generate `AttendanceProposal` and `WaitlistProposal` with per-selection reasoning
- Append immutable audit entries for all decisions
- **NEVER** auto-accept or auto-reject attendees

**Hard Guardrails:**
- ⛔ **NEVER** auto-accept or auto-reject
- ⛔ **NEVER** infer sensitive traits not consented
- ⛔ **NEVER** hide reasoning
- ⛔ **NEVER** send PII to cloud — all processing via Foundry Local (on-device)

**Dependencies:**
- Auditor Agent (audit trail writes)
- Foundry Local (PII processing, on-device SLM)
- FairnessBudget configuration (from Backend)

**Interactions:**
- Receives curation requests from Orchestrator (event ID + registrants)
- Queries Auditor for historical curation patterns
- Emits proposals to Approver for human review

---

### 1.4 Auditor Agent (Reviewer)

**Domain:** Attendee Curation  
**Type:** Reviewer Agent  
**Purpose:** Maintain immutable audit trail for all curation decisions, enabling transparency, accountability, and bias detection.

**Responsibilities:**
- Append audit entries (curation decisions, human overrides, reasoning)
- Query past curation history for continuity signals
- Generate audit reports for organizers
- Flag unusual patterns (e.g., sudden demographic shifts, repeated rejections)
- Store audit log entries (append-only, 2-year retention)

**Dependencies:**
- Audit database (SQL Server, append-only table)
- MCP tool for audit log reads/writes

**Interactions:**
- Invoked by Curator to log decisions
- Queried by Curator for historical context
- Queried by Approver for audit trail visibility

---

### 1.5 Reporter Agent (Lead)

**Domain:** Post-Meetup Reporting  
**Type:** Lead Agent  
**Purpose:** Draft post-event summaries, highlight reels, and action item lists from session notes, attendance data, and feedback.

**Responsibilities:**
- Synthesize session notes and feedback into narrative summaries
- Extract action items with owners and due dates
- Generate highlight reels with attributed quotes (opt-in only)
- Surface aggregate feedback themes and sentiment
- Produce `EventSummary` for human editing

**Dependencies:**
- Facilitator Agent notes (from live sessions)
- Feedback survey data (anonymized aggregates)
- Playwright-captured screenshots (optional, for visual highlights)

**Interactions:**
- Receives reporting requests from Orchestrator (event ID)
- Queries Backend API for session notes and attendance data
- Emits drafts to Approver for human edit pass

---

### 1.6 Orchestrator Agent (Cross-Cutting Coordinator)

**Domain:** All Domains  
**Type:** Coordinator Agent  
**Purpose:** Coordinate multi-agent workflows, enforce sequencing constraints, and manage agent-to-agent communication (A2A) via MAF message bus.

**Responsibilities:**
- Initiate event workflows (planning → curation → reporting)
- Route messages between agents (e.g., Planner → Scout, Curator → Auditor)
- Enforce sequencing: no curation before planning approval, no reporting before event completion
- Maintain workflow state (in-progress, pending approval, completed)
- Surface workflow status to human operators via Aspire Dashboard

**Dependencies:**
- All domain agents
- MAF message bus (agent-to-agent communication)
- Backend API (workflow state persistence)

**Interactions:**
- Spawns Planner on new event creation
- Spawns Curator after planning approval and registrations close
- Spawns Reporter after event completion
- Communicates with Approver to gate phase transitions

---

### 1.7 Approver Agent (Human-in-the-Loop Gateway)

**Domain:** All Domains  
**Type:** Approval Gateway Agent  
**Purpose:** Mediate all human approval workflows: agenda approval, curation review, report edits. Enforces "no autonomous publish" guardrail.

**Responsibilities:**
- Present agent outputs (drafts, proposals, reports) to humans for review
- Capture human edits, approvals, and rejections
- Emit approval signals to Orchestrator to advance workflows
- Log all approval/rejection events to Auditor
- Enforce timeout policies (e.g., draft expires if not approved in 7 days)

**Dependencies:**
- All Lead agents (Planner, Curator, Reporter)
- Backend API (approval state, human annotations)
- Blazor UI (InteractiveServer pages for approval flows)

**Interactions:**
- Receives drafts/proposals from Lead agents
- Renders approval UI for human review
- Emits approval/rejection messages to Orchestrator
- Logs decisions to Auditor

---

## 2. Hosting Strategy (Hosted vs Local)

| Agent | Hosting | Justification | State Persistence |
|-------|---------|---------------|-------------------|
| **Planner** | **Foundry Hosted** | Long-running drafts across multiple sessions; organizers iterate over days. Stateful memory (past events, speaker history) required. | Persistent (keyed by organizer ID) |
| **Scout** | **Local (MAF only)** | Short-lived context gathering; no state retention needed between invocations. | Stateless |
| **Curator** | **Local (MAF only)** | ⚠️ **MUST** be local for PII isolation. Foundry Local (on-device SLM) for all processing. No cloud routing. | Session-scoped (event ID); cleared after approval |
| **Auditor** | **Local (MAF only)** | Append-only writes to SQL; no long-running state. Stateless query interface. | Database-backed (not agent memory) |
| **Reporter** | **Foundry Hosted** | Iterative drafting over multiple editing sessions; organizer feedback loop spans days. | Event-scoped (keyed by event ID) |
| **Orchestrator** | **Foundry Hosted** | Workflow state spans days/weeks (planning → curation → reporting). Needs durable workflow memory. | Persistent (keyed by event ID) |
| **Approver** | **Local (MAF only)** | Stateless gateway; approval state stored in Backend, not agent memory. | Database-backed (not agent memory) |

### Why Foundry Hosted for Planner, Reporter, Orchestrator?

- **Multi-session workflows:** Event planning and reporting span days/weeks. Organizers draft, pause, resume, iterate.
- **Persistent memory:** Planner retains speaker history; Reporter retains draft edits; Orchestrator tracks workflow state.
- **Durable state:** Foundry Hosted provides Redis-backed memory persistence across agent restarts.

### Why Local for Curator?

- **PII Guardrail:** Attendee data (names, DEI fields) must never leave the device. Foundry Local (on-device SLM) processes all curation logic offline.
- **Trust Boundary:** Local-only execution enforces the privacy firewall — no cloud provider sees PII.

### Why Local for Scout, Auditor, Approver?

- **Scout:** Stateless context fetcher. No need for persistent memory.
- **Auditor:** Database-backed audit log. Agent is a thin query/write interface.
- **Approver:** Approval state lives in Backend (SQL). Agent is UI gateway, not state store.

---

## 3. Folder Structure

### 3.1 Agent Implementations (`.agents/`)

```
.agents/
├─ planner/
│  ├─ PlannerAgent.cs              # MAF IAgent implementation
│  ├─ PlannerTools.cs              # Tool definitions (GetEventHistory, DraftAgenda, etc.)
│  ├─ PlannerPrompts.cs            # System prompt templates
│  ├─ PlannerMemory.cs             # Foundry Hosted memory config (organizer-keyed)
│  └─ README.md                    # Agent charter (mirrors .github/agents/planner.md)
│
├─ scout/
│  ├─ ScoutAgent.cs
│  ├─ ScoutTools.cs                # GetSpeakerAvailability, GetVenueConstraints, etc.
│  ├─ ScoutPrompts.cs
│  └─ README.md
│
├─ curator/
│  ├─ CuratorAgent.cs              # ⚠️ [RequiresLocalProvider] attribute on all tools
│  ├─ CuratorTools.cs              # ScoreThemeAlignment (on-device), ProposeCurationResult
│  ├─ CuratorPrompts.cs            # Foundry Local prompt templates
│  ├─ CuratorMemory.cs             # Session-scoped (event ID), cleared post-approval
│  ├─ FairnessBudget.cs            # Domain logic for DEI nudges
│  └─ README.md                    # ⚠️ PII guardrails explicitly documented
│
├─ auditor/
│  ├─ AuditorAgent.cs
│  ├─ AuditorTools.cs              # AppendAuditEntry, QueryAuditHistory
│  ├─ AuditorPrompts.cs
│  └─ README.md
│
├─ reporter/
│  ├─ ReporterAgent.cs
│  ├─ ReporterTools.cs             # DraftSummary, ExtractActionItems, GetScreenshots
│  ├─ ReporterPrompts.cs
│  ├─ ReporterMemory.cs            # Foundry Hosted (event-keyed), retains drafts
│  └─ README.md
│
├─ orchestrator/
│  ├─ OrchestratorAgent.cs
│  ├─ OrchestratorTools.cs         # SpawnAgent, RouteMessage, AdvanceWorkflow
│  ├─ OrchestratorPrompts.cs
│  ├─ OrchestratorMemory.cs        # Foundry Hosted (event-keyed), workflow state
│  └─ README.md
│
└─ approver/
   ├─ ApproverAgent.cs
   ├─ ApproverTools.cs             # PresentForApproval, CaptureEdits, EmitApproval
   ├─ ApproverPrompts.cs
   └─ README.md
```

### 3.2 Copilot Skills (`copilot/skills/`)

Existing skills (no changes needed):
- `seed-db/` — seed dev data (Backend-dependent)
- `curate-attendees/` — scaffold Curator pipeline + TUnit stubs
- `run-e2e/` — Playwright E2E suite runner
- `run-tunit/` — TDD loop with watch mode
- `scaffold-agent/` — scaffold new MAF agent structure

**New skills to add:**

```
copilot/skills/
├─ run-orchestration/              # NEW
│  ├─ SKILL.md
│  └─ skill.json
│
└─ audit-curation/                 # NEW
   ├─ SKILL.md
   └─ skill.json
```

**`run-orchestration/SKILL.md`** — Trigger event orchestration workflow (planning → curation → reporting) for a test event. Useful for end-to-end validation.

**`audit-curation/SKILL.md`** — Query Auditor for curation history and bias detection reports. Surfaces anomalies (demographic shifts, repeated rejections).

### 3.3 MCP Service Boundaries

Agent-to-agent communication (A2A) uses MAF message bus. External service calls use MCP tools:

```
src/Bethuya.Agents/
├─ Mcp/
│  ├─ EventHistoryMcp.cs           # MCP tool: query past events from Backend API
│  ├─ SpeakerAvailabilityMcp.cs   # MCP tool: query speaker calendars (external APIs)
│  ├─ VenueConstraintsMcp.cs      # MCP tool: fetch venue data (external APIs)
│  ├─ AuditLogMcp.cs               # MCP tool: append/query audit log (SQL Server)
│  ├─ SessionNotesMcp.cs           # MCP tool: fetch Facilitator notes (Backend API)
│  └─ FeedbackDataMcp.cs           # MCP tool: fetch anonymized feedback (Backend API)
```

**MCP Tool Naming Convention:**
- `*Mcp.cs` — MAF tool wrapper around external service (HTTP, SQL, GraphQL)
- Tools follow Refit pattern: shared interface in `Bethuya.Hybrid.Shared`, implementation in `Bethuya.Agents`

---

## 4. Agent Responsibility Contracts

### 4.1 Planner Agent

**Inputs:**
- `PlanEventRequest { EventTheme, DateRange, VenueConstraints, TargetCapacity }`
- Historical event data (`List<PastEvent>`)
- Speaker suggestions (`List<SpeakerProfile>`)

**Outputs:**
- `AgendaDraft { Sessions: List<SessionSlot>, Speakers: List<SpeakerAssignment>, Constraints: ConstraintSummary }`
- `SpeakerSuggestions { RankedSpeakers: List<SpeakerSuggestion>, Rationale: string }`

**State:**
- Persistent memory (Foundry Hosted):
  - Past event patterns (successful formats, poorly-rated sessions)
  - Speaker history (acceptance rates, past performance)
  - Organizer preferences (preferred session lengths, break durations)

**Communication:**
- **A2A (via MAF):** Receives `PlanEventCommand` from Orchestrator
- **A2A (via MAF):** Requests context from Scout (`GetSpeakerAvailabilityQuery`)
- **MCP Tool:** Queries `EventHistoryMcp` for past events
- **A2A (via MAF):** Emits `AgendaDraft` to Approver for human review

**Example Flow:**
```
Orchestrator
    │
    └─> Planner: PlanEventCommand { EventId, Theme, DateRange }
        │
        ├─> Scout: GetSpeakerAvailabilityQuery { SpeakerIds, DateRange }
        │   └─> Scout returns SpeakerAvailability
        │
        ├─> EventHistoryMcp: GetPastEvents { Theme: "GenAI", Limit: 10 }
        │   └─> Returns List<PastEvent>
        │
        └─> Planner emits AgendaDraft to Approver
```

---

### 4.2 Scout Agent

**Inputs:**
- `GetSpeakerAvailabilityQuery { SpeakerIds, DateRange }`
- `GetVenueConstraintsQuery { VenueId }`
- `GetCommunitySignalsQuery { DateRange, Region }`

**Outputs:**
- `SpeakerAvailability { SpeakerId, IsAvailable, ConflictingEvents }`
- `VenueConstraints { Capacity, AVEquipment, AccessibilityFeatures }`
- `CommunitySignals { InterestTopics, CompetingEvents, DiscussionVolume }`

**State:**
- Stateless (no persistent memory)

**Communication:**
- **A2A (via MAF):** Receives queries from Planner
- **MCP Tools:** Calls external APIs (calendar services, venue databases, social platforms)
- **A2A (via MAF):** Returns structured data to Planner

---

### 4.3 Curator Agent ⚠️ PII-Sensitive

**Inputs:**
- `CurateAttendeesRequest { EventId, Registrants: List<RegistrantProfile>, FairnessBudget }`
- `RegistrantProfile { Name, ThemeInterest, DEIFields: ConsentedDEIData, CommunityHistory }`
- `FairnessBudget { DiversityTargets, FirstTimeWeight, ContinuityWeight }`

**Outputs:**
- `AttendanceProposal { Attendees: List<SelectionDecision>, Waitlist: List<WaitlistDecision> }`
- `SelectionDecision { RegistrantId, Reason: string, ThemeScore: float, FairnessBudgetImpact }`
- `CurationInsights { DEINudges, EquityFlags, ThemeAlignmentDistribution }`

**State:**
- Session-scoped (keyed by event ID)
- Cleared after human approval (no cross-event retention for PII safety)

**Communication:**
- **A2A (via MAF):** Receives `CurateAttendeesCommand` from Orchestrator
- **⚠️ Foundry Local ONLY:** All PII processing uses on-device SLM (no cloud routing)
- **A2A (via MAF):** Queries Auditor for historical curation patterns (aggregate only, no PII)
- **A2A (via MAF):** Emits proposals to Approver for human review
- **A2A (via MAF):** Logs all decisions to Auditor (`AppendAuditEntryCommand`)

**Example Flow (PII Firewall Enforced):**
```
Orchestrator
    │
    └─> Curator: CurateAttendeesCommand { EventId, Registrants: [...] }
        │
        ├─> [ON-DEVICE ONLY] Foundry Local: ScoreThemeAlignment
        │   └─> Returns List<ThemeScore> (processed locally, never leaves device)
        │
        ├─> Auditor: GetPastCurationHistory { EventId }
        │   └─> Returns aggregate patterns (no PII)
        │
        ├─> [ON-DEVICE ONLY] Apply FairnessBudget logic
        │
        ├─> Curator emits AttendanceProposal to Approver
        │
        └─> Auditor: AppendAuditEntry { EventId, Decisions: [...] }
```

**Privacy Boundary Enforcement:**
- All tools in `CuratorTools.cs` are marked with `[RequiresLocalProvider]` attribute
- MAF runtime rejects any cloud routing for Curator tools
- Foundry Local (on-device) is the only allowed provider

---

### 4.4 Auditor Agent

**Inputs:**
- `AppendAuditEntryCommand { EventId, Action, Actor, Reason, Timestamp }`
- `GetPastCurationHistoryQuery { EventId, Limit }`

**Outputs:**
- `AuditEntry { Id, EventId, Action, Actor, Reason, Timestamp }`
- `CurationHistoryReport { AggregatePatterns, BiasFlagCount, UnusualShifts }`

**State:**
- Database-backed (SQL Server, append-only audit table)
- No agent memory (stateless query interface)

**Communication:**
- **A2A (via MAF):** Receives `AppendAuditEntryCommand` from Curator
- **MCP Tool:** Writes to `AuditLogMcp` (SQL Server)
- **A2A (via MAF):** Responds to queries from Curator and Approver

**Audit Table Schema (SQL Server):**
```sql
CREATE TABLE CurationAuditLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    Actor NVARCHAR(100) NOT NULL,
    Reason NVARCHAR(MAX) NOT NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    INDEX IX_EventId (EventId)
);
```

---

### 4.5 Reporter Agent

**Inputs:**
- `GenerateReportRequest { EventId }`
- Session notes (from Facilitator or manual entry)
- Attendance data (aggregate, anonymized)
- Feedback survey responses

**Outputs:**
- `EventSummary { Narrative, KeyThemes, Outcomes }`
- `HighlightReel { TopMoments, QuotesWithAttribution, Screenshots }`
- `ActionItems { Task, Owner, DueDate }`

**State:**
- Event-scoped memory (Foundry Hosted)
- Retains draft history for iterative editing

**Communication:**
- **A2A (via MAF):** Receives `GenerateReportCommand` from Orchestrator
- **MCP Tools:** Queries `SessionNotesMcp`, `FeedbackDataMcp` from Backend API
- **A2A (via MAF):** Emits drafts to Approver for human edit pass

---

### 4.6 Orchestrator Agent

**Inputs:**
- `InitiateEventWorkflowCommand { EventId, WorkflowType }`
- Approval signals from Approver (`AgendaApproved`, `CurationApproved`, `ReportApproved`)

**Outputs:**
- Workflow state updates (`InProgress`, `PendingApproval`, `Completed`)
- Agent spawn commands (to Planner, Curator, Reporter)

**State:**
- Persistent workflow state (Foundry Hosted, keyed by event ID)
- Tracks phase transitions: Planning → Curation → Reporting

**Communication:**
- **A2A (via MAF):** Spawns agents (Planner, Curator, Reporter) based on workflow phase
- **A2A (via MAF):** Receives approval signals from Approver
- **MCP Tool:** Queries Backend API for workflow state persistence

**Workflow State Machine:**
```
[Event Created]
    │
    v
[Planning Phase]  <────┐
    │                  │ (rejection)
    ├─> Planner spawned│
    │                  │
    v                  │
[Pending Agenda Approval] ─┘
    │
    │ (approval)
    v
[Registration Open]
    │
    v
[Curation Phase]  <────┐
    │                  │ (rejection)
    ├─> Curator spawned│
    │                  │
    v                  │
[Pending Curation Approval] ─┘
    │
    │ (approval)
    v
[Event Running]
    │
    v
[Reporting Phase]
    │
    ├─> Reporter spawned
    │
    v
[Pending Report Approval]
    │
    │ (approval)
    v
[Event Completed]
```

---

### 4.7 Approver Agent

**Inputs:**
- Agent outputs requiring approval (`AgendaDraft`, `AttendanceProposal`, `EventSummary`)
- Human edits and approval/rejection signals

**Outputs:**
- Approval/rejection messages to Orchestrator
- Audit log entries (via Auditor)

**State:**
- Database-backed (SQL Server, approval state table)
- No agent memory (stateless gateway)

**Communication:**
- **A2A (via MAF):** Receives drafts from Planner, Curator, Reporter
- **Blazor UI:** Renders approval pages (InteractiveServer render mode)
- **A2A (via MAF):** Emits approval signals to Orchestrator
- **A2A (via MAF):** Logs decisions to Auditor

---

## 5. Identity, Scaling & Persistence

### 5.1 Agent Identity & Versioning

Agents are versioned using **mnemonic names + semantic versioning** for clarity and rollback safety:

```
planner-v1.0              # Initial release
planner-v1.1              # Bug fix: improved speaker scoring
curator-pii-safe-v2.0     # Major version: PII firewall hardening
reporter-visual-v1.2      # Minor version: added screenshot support
```

**Identity Strategy:**
- **Development:** Mnemonic names without versions (`planner`, `curator`) for rapid iteration
- **Staging/Production:** Versioned endpoints (`planner-v1.0`) for A/B testing and rollback
- **Routing:** Orchestrator uses Aspire service discovery to resolve agent endpoints (`https+http://planner`)

**Why Mnemonic Names?**
- Human-readable logs and traces in Aspire Dashboard
- Clear separation of agent roles in A2A message routing
- Easy to reason about workflows: `Orchestrator → planner → scout → Approver`

### 5.2 Scaling Strategy

| Agent | Scaling Approach | Justification |
|-------|------------------|---------------|
| **Planner** | Single instance (no fan-out) | One draft per event; sequential human review |
| **Scout** | Parallel fan-out (per speaker) | Independent external API calls; no shared state |
| **Curator** | Single instance (no fan-out) | Sequential curation; human must review as a coherent whole |
| **Auditor** | Single instance (append-only) | SQL writes are serialized; no contention on reads |
| **Reporter** | Single instance (no fan-out) | One report per event; sequential editing |
| **Orchestrator** | Single instance per event | Workflow coordination requires centralized state |
| **Approver** | Single instance (no fan-out) | Human approval is sequential; no parallel gating |

**Fan-Out Example: Scout**

When Planner needs speaker availability for 10 speakers, Scout spawns 10 parallel queries:

```
Planner
    │
    └─> Scout: GetSpeakerAvailability { SpeakerIds: [1, 2, ..., 10] }
        │
        ├─> [Parallel] Query calendar API for Speaker 1
        ├─> [Parallel] Query calendar API for Speaker 2
        ├─> ...
        └─> [Parallel] Query calendar API for Speaker 10
        │
        └─> Aggregate results → return to Planner
```

**Curation Rounds (Sequential, Not Parallel):**

Curator processes registrants sequentially to ensure FairnessBudget coherence. Parallel curation would create race conditions on diversity targets.

### 5.3 Persistence Layers

| Data Type | Storage | Rationale |
|-----------|---------|-----------|
| **Registrant PII** | ⚠️ **Local SQLite only** (Foundry Local) | Never leaves device; deleted after curation approval |
| **Audit log** | SQL Server (append-only table) | 2-year retention; immutable; compliance-ready |
| **Workflow state** | Redis (via Foundry Hosted) | Orchestrator state spans days/weeks |
| **Agent memory** | Redis (via Foundry Hosted) | Planner/Reporter drafts persist across sessions |
| **Approval state** | SQL Server (approval table) | Durable approval records; queryable for reports |
| **Session notes** | SQL Server (notes table) | Facilitator-captured notes; linked to event ID |
| **Feedback data** | SQL Server (feedback table) | Anonymized survey responses; aggregate analytics |

**Redis Backing (Foundry Hosted):**
- Planner memory: `planner:organizer:{organizerId}` (TTL: 30 days)
- Reporter memory: `reporter:event:{eventId}` (TTL: 60 days)
- Orchestrator state: `workflow:event:{eventId}` (TTL: 90 days)

**SQL Server Schema (Key Tables):**

```sql
-- Audit log (append-only)
CREATE TABLE CurationAuditLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    Actor NVARCHAR(100) NOT NULL,
    Reason NVARCHAR(MAX) NOT NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    INDEX IX_EventId (EventId)
);

-- Approval state
CREATE TABLE ApprovalState (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    EventId UNIQUEIDENTIFIER NOT NULL,
    WorkflowPhase NVARCHAR(50) NOT NULL,
    Status NVARCHAR(20) NOT NULL,  -- Pending, Approved, Rejected
    Approver NVARCHAR(100) NULL,
    ApprovedAt DATETIME2 NULL,
    Edits NVARCHAR(MAX) NULL,
    INDEX IX_EventId (EventId),
    INDEX IX_Status (Status)
);

-- Workflow state (redundant with Redis, for durability)
CREATE TABLE WorkflowState (
    EventId UNIQUEIDENTIFIER PRIMARY KEY,
    CurrentPhase NVARCHAR(50) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    LastUpdated DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

---

## 6. Privacy Boundary Architecture

### 6.1 PII Firewall

```
┌────────────────────────────────────────────────────────────┐
│                      TRUST BOUNDARY                        │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  ┌──────────────────────────────────────────────────┐    │
│  │         LOCAL (On-Device) — PII ZONE             │    │
│  │                                                  │    │
│  │  ┌───────────┐     ┌──────────────────────┐    │    │
│  │  │  Curator  │ ───▶│  Foundry Local SLM  │    │    │
│  │  │   Agent   │     │  (phi-4-mini, etc.)  │    │    │
│  │  └───────────┘     └──────────────────────┘    │    │
│  │        │                                        │    │
│  │        v                                        │    │
│  │  [SQLite PII Store]                            │    │
│  │  (deleted after approval)                      │    │
│  │                                                  │    │
│  └──────────────────────────────────────────────────┘    │
│                                                            │
│  ════════════════════════════════════════════════         │
│                    NO PII CROSSES                         │
│  ════════════════════════════════════════════════         │
│                                                            │
│  ┌──────────────────────────────────────────────────┐    │
│  │         CLOUD (Azure) — NON-PII ZONE             │    │
│  │                                                  │    │
│  │  ┌───────────┐     ┌──────────────────────┐    │    │
│  │  │  Planner  │ ───▶│  Azure OpenAI GPT-4o │    │    │
│  │  │  Reporter │     │                      │    │    │
│  │  └───────────┘     └──────────────────────┘    │    │
│  │        │                                        │    │
│  │        v                                        │    │
│  │  [SQL Server: Audit Log, Approval State]      │    │
│  │  (no PII; aggregate data only)                 │    │
│  │                                                  │    │
│  └──────────────────────────────────────────────────┘    │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

### 6.2 Routing Rules for Attendee Data

| Data Type | Allowed Destinations | Prohibited Destinations |
|-----------|---------------------|-------------------------|
| Registrant name, email, phone | ⚠️ **Local SQLite only** | ❌ Azure SQL, Redis, Azure OpenAI, OpenAI |
| Consented DEI fields | ⚠️ **Local SQLite only** | ❌ Any cloud provider |
| Theme alignment scores | ✅ Auditor (aggregate only) | ❌ Individual scores to cloud |
| Curation decisions | ✅ Auditor (audit log) | ❌ With PII identifiers |
| Aggregate DEI statistics | ✅ Backend API, Auditor | ✅ Safe for cloud storage |

**Enforcement Mechanisms:**

1. **Attribute-based routing:** All Curator tools marked with `[RequiresLocalProvider]` attribute. MAF runtime enforces local-only execution.

2. **Data sanitization:** Auditor receives only sanitized records:
   ```csharp
   // ❌ WRONG: PII in audit log
   AppendAuditEntry(eventId, "Selected Jane Doe because high theme score");

   // ✅ CORRECT: No PII in audit log
   AppendAuditEntry(eventId, $"Selected registrant {registrantId} because high theme score");
   ```

3. **Local storage deletion:** SQLite PII store is deleted after human approval via Approver agent:
   ```csharp
   public async Task OnApprovalAsync(EventId eventId)
   {
       await _curatorLocalStore.DeletePIIAsync(eventId);
       await _auditor.AppendAuditEntryAsync(eventId, "PII deleted post-approval", "Approver");
   }
   ```

### 6.3 Audit Trail Design

All curation decisions (both agent proposals and human overrides) are logged to the immutable audit trail:

**Audit Entry Schema:**
```csharp
public record AuditEntry(
    long Id,
    EventId EventId,
    string Action,       // "AgentProposal", "HumanOverride", "PIIDeleted"
    string Actor,        // "Curator", "Organizer Alice", "Approver"
    string Reason,       // Sanitized explanation (no PII)
    DateTime Timestamp
);
```

**Example Audit Log:**
```
Id  | EventId | Action         | Actor         | Reason                          | Timestamp
----|---------|----------------|---------------|---------------------------------|-------------------
1   | abc-123 | AgentProposal  | Curator       | Selected 50 attendees           | 2026-04-20 10:00
2   | abc-123 | HumanOverride  | Organizer Bob | Moved registrant X to waitlist  | 2026-04-20 10:30
3   | abc-123 | HumanApproval  | Organizer Bob | Approved curation list          | 2026-04-20 11:00
4   | abc-123 | PIIDeleted     | Approver      | Deleted local PII store         | 2026-04-20 11:01
```

**Audit Queries (via Auditor Agent):**
- "Show all overrides for Event abc-123"
- "Flag events with >30% override rate" (bias detection)
- "List all curation decisions for past 6 months"

---

## 7. Integration Points (Aspire, Skills, MCP)

### 7.1 Aspire AppHost Registration

Agents are registered as Aspire projects and connected via `WithReference(...)`:

```csharp
// AppHost/AppHost/Program.cs

var builder = DistributedApplication.CreateBuilder(args);

// Backend API
var backend = builder.AddProject<Projects.Bethuya_Backend>("backend")
    .WithReference(sqlServer)
    .WithReference(redis);

// Agent runtime (MAF host)
var agentRuntime = builder.AddProject<Projects.Bethuya_Agents>("agents")
    .WithReference(backend)
    .WithReference(redis)
    .WithEnvironment("AI_PROVIDER_PLANNER", "AzureOpenAI")
    .WithEnvironment("AI_PROVIDER_CURATOR", "FoundryLocal")
    .WithEnvironment("AI_PROVIDER_REPORTER", "AzureOpenAI");

// Web UI
builder.AddProject<Projects.Bethuya_Hybrid_Web>("web")
    .WithReference(backend)
    .WithReference(agentRuntime);

builder.Build().Run();
```

**Service Discovery:**
- Agents resolve Backend API via `https+http://backend` (Aspire-managed URL)
- Web UI resolves Agent runtime via `https+http://agents` for approval UI

### 7.2 Copilot Skills Integration

Existing skills reference new agents:

**`curate-attendees/SKILL.md` (updated):**
```markdown
## Agent Integration

This skill scaffolds the Curator agent pipeline and TUnit stubs.

After running this skill:
1. `.agents/curator/` folder is populated with agent implementation
2. TUnit tests are generated in `tests/Bethuya.Tests/Agents/CuratorAgentTests.cs`
3. Foundry Local config is added to user-secrets

## Next Steps

1. Run `aspire start` to launch the agent runtime
2. Use the Orchestrator to trigger curation workflow
3. Test via Approver UI at `https://localhost:5001/curation/approve/{eventId}`
```

### 7.3 MCP Interfaces for External Services

Agents use MCP tools to call external services. Example: Scout querying speaker availability.

**Interface Definition (Shared Contract):**
```csharp
// src/Bethuya.Hybrid/Bethuya.Hybrid.Shared/Services/ISpeakerAvailabilityApi.cs

namespace Bethuya.Hybrid.Shared.Services;

public interface ISpeakerAvailabilityApi
{
    [Get("/api/speakers/{speakerId}/availability")]
    Task<SpeakerAvailability> GetAvailabilityAsync(
        SpeakerId speakerId,
        [Query] DateOnly startDate,
        [Query] DateOnly endDate
    );
}
```

**MCP Tool Implementation (Agent Runtime):**
```csharp
// src/Bethuya.Agents/Mcp/SpeakerAvailabilityMcp.cs

namespace Bethuya.Agents.Mcp;

public class SpeakerAvailabilityMcp : IMafTool
{
    private readonly ISpeakerAvailabilityApi _api;

    public SpeakerAvailabilityMcp(ISpeakerAvailabilityApi api)
    {
        _api = api;
    }

    [Tool("GetSpeakerAvailability")]
    public async Task<SpeakerAvailability> GetAvailabilityAsync(
        SpeakerId speakerId,
        DateOnly startDate,
        DateOnly endDate
    )
    {
        return await _api.GetAvailabilityAsync(speakerId, startDate, endDate);
    }
}
```

**Refit Registration (Agent Runtime DI):**
```csharp
// src/Bethuya.Agents/Program.cs

builder.Services.AddRefitClient<ISpeakerAvailabilityApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://calendar.example.com"));

builder.Services.AddSingleton<SpeakerAvailabilityMcp>();
```

### 7.4 Agent-to-Agent Communication (A2A via MAF)

Agents communicate via MAF message bus. Example: Planner requesting context from Scout.

**Message Definition:**
```csharp
// src/Bethuya.Agents/Messages/GetSpeakerAvailabilityQuery.cs

namespace Bethuya.Agents.Messages;

public record GetSpeakerAvailabilityQuery(
    List<SpeakerId> SpeakerIds,
    DateOnly StartDate,
    DateOnly EndDate
) : IAgentMessage;
```

**Scout Handler:**
```csharp
// src/Bethuya.Agents/Scout/ScoutAgent.cs

public class ScoutAgent : IAgent
{
    private readonly SpeakerAvailabilityMcp _speakerAvailabilityMcp;

    [MessageHandler]
    public async Task<SpeakerAvailabilityResponse> HandleAsync(GetSpeakerAvailabilityQuery query)
    {
        var availabilities = new List<SpeakerAvailability>();

        // Parallel fan-out for each speaker
        await Parallel.ForEachAsync(query.SpeakerIds, async (speakerId, ct) =>
        {
            var availability = await _speakerAvailabilityMcp.GetAvailabilityAsync(
                speakerId, query.StartDate, query.EndDate
            );
            availabilities.Add(availability);
        });

        return new SpeakerAvailabilityResponse(availabilities);
    }
}
```

**Planner Invocation:**
```csharp
// src/Bethuya.Agents/Planner/PlannerAgent.cs

public class PlannerAgent : IAgent
{
    private readonly IAgentMessageBus _messageBus;

    [Tool("DraftAgenda")]
    public async Task<AgendaDraft> DraftAgendaAsync(PlanEventRequest request)
    {
        // Query Scout for speaker availability
        var availabilityQuery = new GetSpeakerAvailabilityQuery(
            request.SpeakerIds,
            request.DateRange.Start,
            request.DateRange.End
        );

        var availabilityResponse = await _messageBus.SendAsync<SpeakerAvailabilityResponse>(
            "scout", availabilityQuery
        );

        // Use availability data to draft agenda
        var draft = GenerateDraft(request, availabilityResponse);

        return draft;
    }
}
```

---

## 8. Implementation Roadmap

### Phase 1: Foundation (Weeks 1-2)

**Deliverables:**
- `.agents/` folder structure scaffolded
- Orchestrator + Approver agents (minimal implementations)
- Aspire AppHost registration for agent runtime
- SQL Server tables (audit log, approval state, workflow state)
- TUnit test stubs for Orchestrator + Approver

**Acceptance Criteria:**
- `aspire start` launches agent runtime successfully
- Orchestrator can spawn no-op agents and log to Aspire Dashboard
- Approver can render basic approval UI (no agent integration yet)
- All tests pass; 0 warnings

### Phase 2: Planner + Scout (Weeks 3-4)

**Deliverables:**
- Planner agent implementation with Foundry Hosted memory
- Scout agent implementation (stateless, external API calls)
- MCP tools: `EventHistoryMcp`, `SpeakerAvailabilityMcp`
- Copilot skill: `run-orchestration` (trigger planning workflow)
- TUnit tests: Planner drafts agendas correctly, Scout queries APIs

**Acceptance Criteria:**
- Planner drafts agenda from historical data + Scout context
- Scout queries external APIs in parallel (fan-out)
- Approval UI renders agenda drafts with human edit capability
- Orchestrator advances to next phase on approval

### Phase 3: Curator + Auditor ⚠️ PII-Sensitive (Weeks 5-6)

**Deliverables:**
- Curator agent with Foundry Local enforcement (`[RequiresLocalProvider]`)
- Auditor agent (append-only audit log writes)
- FairnessBudget logic implementation
- MCP tools: `AuditLogMcp`
- Copilot skill: `audit-curation` (query audit log)
- TUnit tests: Curator proposes fair selections, Auditor logs correctly

**Acceptance Criteria:**
- Curator processes registrants on-device only (Foundry Local)
- All PII stays in local SQLite; deleted after approval
- Auditor writes immutable audit entries
- Approval UI renders curation proposals with per-selection reasoning

### Phase 4: Reporter (Weeks 7-8)

**Deliverables:**
- Reporter agent with Foundry Hosted memory
- MCP tools: `SessionNotesMcp`, `FeedbackDataMcp`
- Integration with Playwright screenshots (optional visual highlights)
- TUnit tests: Reporter drafts summaries from notes + feedback

**Acceptance Criteria:**
- Reporter generates event summaries from Facilitator notes
- Approval UI supports iterative editing of drafts
- Orchestrator marks workflow as complete on final approval

### Phase 5: E2E Validation (Week 9)

**Deliverables:**
- Playwright E2E tests for full event lifecycle (planning → curation → reporting)
- Copilot skill: `run-orchestration` (end-to-end test scenario)
- Performance validation: p99 latency < 180ms for hot paths
- Security audit: PII isolation verified, audit log immutable

**Acceptance Criteria:**
- E2E test creates event, drafts agenda, curates attendees, generates report
- All approvals gated by human; no autonomous publish
- PII never leaves device (verified via network capture)
- Audit log contains complete decision trail

---

## 9. Open Questions & Future Work

### 9.1 Open Questions

1. **Speaker Invitation Workflow:** Should Planner automatically email speaker invitations, or require human approval per invitation?
   - **Recommendation:** Require approval. Planner drafts invitation emails; human sends.

2. **Curation Tie-Breaking:** When two registrants have identical scores, how does Curator decide?
   - **Recommendation:** Random selection with explicit reasoning ("tie-breaker: random selection among equal scores").

3. **Facilitator Integration:** How does Facilitator Agent feed notes to Reporter?
   - **Recommendation:** Facilitator writes notes to Backend API; Reporter queries via `SessionNotesMcp`.

4. **Multi-Organizer Approval:** Should approval require consensus (multiple organizers), or single-organizer approval?
   - **Recommendation:** Configurable policy (event-level setting). Default: single organizer.

### 9.2 Future Work

1. **Bias Detection:** Automated anomaly detection in audit logs (e.g., repeated rejections of specific demographics).
   - **Implementation:** Auditor emits weekly reports to organizers flagging unusual patterns.

2. **Feedback Loop:** Integrate post-event feedback into Planner memory to improve future agendas.
   - **Implementation:** Reporter extracts "what worked / what didn't" and appends to Planner's historical context.

3. **Speaker Reputation Scoring:** Track speaker performance across events to improve Planner suggestions.
   - **Implementation:** Backend API aggregates feedback per speaker; Planner queries via MCP.

4. **Multi-Event Orchestration:** Support recurring event series (e.g., monthly meetups) with template-based planning.
   - **Implementation:** Planner retains "event template" memory; Orchestrator spawns Planner with template context.

---

## 10. References & Dependencies

### 10.1 Documentation

- **AGENTS.md** — Domain agents (Planner, Curator, Facilitator, Reporter), AI provider routing
- **CLAUDE.md** — Mirror reference for Claude models
- **.github/agents/** — Agent persona files (planner.md, curator.md, facilitator.md, reporter.md)
- **.squad/routing.md** — Work routing table (who handles what)
- **.squad/decisions.md** — Architectural decisions, evidence policy

### 10.2 External Dependencies

- **Microsoft Agent Framework (MAF)** — Agent runtime, tools, memory, A2A messaging
- **Microsoft Foundry** — Hosted agents (Planner, Reporter, Orchestrator)
- **Foundry Local** — On-device SLM for PII processing (Curator)
- **.NET Aspire 13** — Orchestration, service discovery, observability
- **Refit** — Type-safe HTTP clients for MCP tools
- **Vogen** — Identity value objects (EventId, SpeakerId, AttendeeId)
- **TUnit** — Unit/integration testing
- **Playwright for .NET** — E2E testing

### 10.3 Infrastructure Requirements

- **SQL Server** — Audit log, approval state, workflow state
- **Redis** — Foundry Hosted memory persistence (agent state)
- **SQLite** — Local PII store (Curator, device-only)
- **Azure OpenAI** — Planner, Reporter (non-sensitive content)
- **Foundry Local** — Curator (PII processing, on-device)

---

## Appendix A: Agent Pseudo-Code Examples

### A.1 Planner Tool: DraftAgenda

```csharp
[Tool("DraftAgenda")]
public async Task<AgendaDraft> DraftAgendaAsync(PlanEventRequest request)
{
    // 1. Query historical events
    var pastEvents = await _eventHistoryMcp.GetPastEventsAsync(request.Theme, limit: 10);

    // 2. Query Scout for speaker availability
    var availabilityQuery = new GetSpeakerAvailabilityQuery(
        request.SpeakerIds, request.DateRange.Start, request.DateRange.End
    );
    var availability = await _messageBus.SendAsync<SpeakerAvailabilityResponse>(
        "scout", availabilityQuery
    );

    // 3. Generate draft agenda using LLM
    var prompt = $@"
        Event Theme: {request.Theme}
        Date Range: {request.DateRange}
        Venue Capacity: {request.VenueConstraints.Capacity}
        Available Speakers: {availability.ToSummary()}
        Past Successful Formats: {pastEvents.ToSummary()}

        Draft a 4-hour agenda with:
        - Opening keynote (30 min)
        - 2-3 technical sessions (45 min each)
        - Q&A breaks (15 min each)
        - Networking lunch (60 min)
        - Closing remarks (15 min)

        Optimize for: high engagement, diverse speakers, realistic timing.
    ";

    var draft = await _llm.CompletionAsync(prompt);

    // 4. Validate constraints
    var violations = ValidateConstraints(draft, request.VenueConstraints);
    if (violations.Any())
    {
        draft.ConstraintSummary = new ConstraintSummary(violations);
    }

    return draft;
}
```

### A.2 Curator Tool: ProposeCurationResult

```csharp
[Tool("ProposeCurationResult")]
[RequiresLocalProvider]  // ⚠️ Enforces Foundry Local routing
public async Task<AttendanceProposal> ProposeCurationResultAsync(CurateAttendeesRequest request)
{
    // 1. Score theme alignment (on-device ML)
    var themeScores = await _foundryLocal.ScoreThemeAlignmentAsync(
        request.Registrants, request.EventTheme
    );

    // 2. Query historical curation patterns (aggregate only, no PII)
    var historicalPatterns = await _auditor.GetPastCurationHistoryAsync(request.EventId);

    // 3. Apply FairnessBudget logic
    var budget = request.FairnessBudget;
    var selections = new List<SelectionDecision>();
    var waitlist = new List<WaitlistDecision>();

    foreach (var registrant in request.Registrants.OrderByDescending(r => themeScores[r.Id]))
    {
        if (selections.Count < request.Capacity)
        {
            var reason = $"High theme alignment (score: {themeScores[registrant.Id]:F2})";
            if (registrant.IsFirstTime) reason += "; first-time attendee";
            if (budget.ShouldNudgeDiversity(registrant, selections))
                reason += "; supports diversity targets";

            selections.Add(new SelectionDecision(registrant.Id, reason));
        }
        else
        {
            waitlist.Add(new WaitlistDecision(registrant.Id, "Capacity reached"));
        }
    }

    // 4. Generate insights
    var insights = new CurationInsights
    {
        DEINudges = budget.GetDEINudges(selections),
        EquityFlags = budget.GetEquityFlags(selections),
        ThemeAlignmentDistribution = themeScores.GroupBy(s => s.Value / 10).ToList()
    };

    return new AttendanceProposal(selections, waitlist, insights);
}
```

### A.3 Orchestrator: Advance Workflow on Approval

```csharp
[MessageHandler]
public async Task HandleAsync(AgendaApprovedEvent evt)
{
    // 1. Update workflow state
    var workflow = await _workflowStateMcp.GetAsync(evt.EventId);
    workflow.CurrentPhase = WorkflowPhase.RegistrationOpen;
    await _workflowStateMcp.UpdateAsync(workflow);

    // 2. Log approval to Auditor
    await _auditor.AppendAuditEntryAsync(
        evt.EventId,
        "AgendaApproved",
        evt.Approver,
        "Organizer approved planning phase"
    );

    // 3. Notify Backend to open registration
    await _backend.OpenRegistrationAsync(evt.EventId);

    // 4. Schedule Curator spawn (wait for registration close)
    var registrationCloseDate = evt.Event.RegistrationCloseDate;
    _scheduler.Schedule(registrationCloseDate, async () =>
    {
        await SpawnCuratorAsync(evt.EventId);
    });
}
```

---

## Appendix B: Security Checklist

- [ ] All Curator tools marked with `[RequiresLocalProvider]`
- [ ] PII never leaves device (verified via network capture)
- [ ] Audit log is append-only (no UPDATE or DELETE SQL permissions)
- [ ] Approval pages use `@rendermode InteractiveServer` (no WASM)
- [ ] All agent-to-agent messages use MAF bus (no raw HTTP)
- [ ] SQL injection: all queries use parameterized statements
- [ ] Rate limiting: 20 req/min for AI endpoints (via ServiceDefaults)
- [ ] Authentication: all agent endpoints require `[Authorize]`
- [ ] Authorization: Curator/Approver require `RequireCurator` policy
- [ ] Secrets: all AI provider keys in Azure Key Vault (not appsettings.json)

---

## Appendix C: Aspire Dashboard Integration

Agents are observable via Aspire Dashboard:

- **Resource View:** Shows agent runtime health, memory usage, CPU
- **Distributed Tracing:** A2A message flows visualized (Planner → Scout → Approver)
- **Logs:** Structured logs from all agents, queryable by event ID
- **Metrics:** Agent invocation counts, latency percentiles, error rates

**Example Dashboard Query:**
```kql
traces
| where cloud_RoleName == "agents"
| where operation_Name == "DraftAgenda"
| summarize p50 = percentile(duration, 50), p99 = percentile(duration, 99) by bin(timestamp, 1h)
```

---

**End of Design Document**
