# Phase 1 Approver Approval UI — Component Structure and State Flow

**Author:** Trinity (Frontend Dev)  
**Date:** 2026-04-28  
**Status:** Implemented  
**Scope:** Phase 1 human-in-the-loop approval UI for Planner, Curator, and Reporter agents

---

## Decision

Implemented Blazor-based approval UI for Phase 1 human review gates using `@rendermode InteractiveServer`, Blazor Blueprint components exclusively, and stub Backend API integration.

## Component Architecture

### Pages Created

```
src/Bethuya.Hybrid/Bethuya.Hybrid.Shared/Pages/Approvals/
├─ Index.razor                      # List pending approvals
├─ PlanApproval.razor              # Agenda review + edit
├─ CurationApproval.razor          # Attendee list approval (aggregate only)
├─ ReportApproval.razor            # Event summary editing
├─ PlanApproval.razor.css          # Shared CSS for all approval pages
└─ Models/
   └─ ApprovalViewModel.cs         # View models for all approval flows
```

### State Flow

```
Approvals Index (/approvals)
  │
  ├─ GET /api/approvals/pending → List<ApprovalViewModel>
  │   (WorkflowPhase: "Planning" | "Curation" | "Reporting")
  │
  └─ User clicks "Review" → Navigate to phase-specific page
      │
      ├─ /approvals/plan/{eventId}
      │   GET /api/approvals/{eventId}/plan/pending → PlanDraftViewModel
      │   User edits sessions, approves or rejects
      │   POST /api/approvals/{eventId}/approve { WorkflowPhase: "Planning", Edits: {...} }
      │
      ├─ /approvals/curation/{eventId}
      │   GET /api/approvals/{eventId}/curation/pending → CurationProposalViewModel
      │   ⚠️ NO PII — only aggregate stats (counts, percentages)
      │   POST /api/approvals/{eventId}/approve { WorkflowPhase: "Curation" }
      │   → Backend MUST delete local PII SQLite store after approval
      │
      └─ /approvals/report/{eventId}
          GET /api/approvals/{eventId}/report/pending → ReportDraftViewModel
          User edits summary, highlights, action items
          POST /api/approvals/{eventId}/approve { WorkflowPhase: "Reporting", Edits: {...} }
```

## Privacy Guardrail (Curation)

Per `.squad/designs/event-orchestration-agents.md` Section 6.2:

- **CurationApproval.razor shows ZERO PII** — only aggregate statistics:
  - Proposed attendee count
  - Waitlist count
  - First-time attendee percentage
  - Underrepresented backgrounds percentage
  - Sanitized fairness budget summary
- Backend API endpoint `/api/approvals/{eventId}/curation/pending` MUST NOT return individual attendee names, emails, or DEI fields.
- Prominent privacy alert displayed on page: "Individual attendee names, emails, and DEI fields are processed locally (Foundry Local) and are not shown in this UI."

## Blazor Blueprint Usage

All components use BB exclusively:
- **Form fields with wrappers:** `BbFormFieldInput`, `BbFormFieldSelect`, `BbFormSection`
- **Standalone components (manual form-group):** `BbTextarea`, `BbDatePicker` (no `BbFormFieldTextarea`/`BbFormFieldDatePicker` wrappers in BB 3.x)
- **UI primitives:** `BbCard`, `BbButton`, `BbAlert`, `BbBadge`, `BbInput`, `BbLabel`
- **Icons:** `LucideIcon` (sparkles, loader-circle, check-circle, list-checks, shield-check, inbox, x, plus)

Custom CSS limited to:
- Page wrappers (max-width centering)
- Layout grids (stats grid, two-column form rows)
- List styling (highlights, action items)
- All styled with theme variables (`--card`, `--border`, `--muted-foreground`)

## Render Mode

All approval pages use `@rendermode InteractiveServer` — approval workflows are sensitive, must run server-side (no WASM client inspection).

## E2E Test Selectors

All interactive elements have `data-test` attributes:
- `plan-approval-page`, `curation-approval-page`, `report-approval-page`, `approvals-page`
- `approve-btn`, `reject-btn`, `reject-curation`, `approve-curation`, `publish-report`, `return-for-edits`
- `session-title-{id}`, `session-speaker-{id}`, `session-starttime-{id}`, `session-endtime-{id}`
- `highlight-{index}`, `highlight-input-{index}`, `remove-highlight-{index}`
- `action-item-{index}`, `action-item-input-{index}`, `remove-action-item-{index}`
- `loading-state`, `not-found-state`, `submit-error`

## Backend API Integration (Stub)

TODO comments mark where real Backend API calls must replace stub logic:
- `GET /api/approvals/pending` → pending approvals list
- `GET /api/approvals/{eventId}/{phase}/pending` → phase-specific draft
- `POST /api/approvals/{eventId}/approve` → record approval + edits
- `POST /api/approvals/{eventId}/reject` → record rejection + reason

## Learnings

- **Razor string interpolation syntax:** Use `@($"prefix-{variable}")` for attributes, not `attr="prefix-@variable"` (RZ9986 error).
- **Lambda return type in Razor foreach:** `Select((item, index) => (item, index))` causes return type mismatch in Razor compiler — use explicit `for` loop with captured `index` instead.
- **BB ButtonSize enum:** Use `ButtonSize.Small`, not `ButtonSize.Sm`.
- **Nullable type params:** Avoid `TValue="string?"` in form fields — causes CS8669 nullable annotation errors. Use `TValue="string"` with non-nullable model properties (empty string defaults).

## Impact

- **Neo (Lead Architect):** Backend API endpoints needed for approval persistence (SQL ApprovalState table).
- **Switch (Backend Dev):** Implement `/api/approvals/*` endpoints matching stub contracts.
- **Tank (Backend Dev):** Ensure curation endpoint returns only aggregate data (no PII).
- **Playwright E2E:** All approval flows ready for E2E testing with `data-test` selectors.

## Next Steps (Phase 2)

- Integrate real Backend API (replace stub TODO comments)
- Add Approver Agent MCP gateway (read approval state, emit signals to Orchestrator)
- Wire Orchestrator to advance workflows on approval/rejection events
- Add approval timeout policy (draft expires if not approved in 7 days)
- Capture Playwright screenshots for visual regression testing
