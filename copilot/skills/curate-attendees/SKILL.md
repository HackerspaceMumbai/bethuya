# Skill: curate-attendees

## Description
Scaffold the Curator Agent pipeline and associated TUnit tests for a given event. Generates the MAF agent structure, FairnessBudget setup, output models, and test stubs.

## Trigger
Use when implementing or extending the Curator Agent. Invoke with `/curate-attendees` in Copilot CLI.

## Prerequisites
- `src/Bethuya.Agents/` project exists (or create it first)
- `src/Bethuya.Core/` project exists with domain models
- `tests/Bethuya.TUnit.Tests/` exists (current test project location)
- Microsoft Agent Framework package referenced in `Directory.Packages.props`

## Steps
1. Read existing `src/Bethuya.Agents/CuratorAgent.cs` (if present) to understand current state.
2. Scaffold the MAF `IAgent` implementation:
   - Class: `CuratorAgent` in `src/Bethuya.Agents/`
   - Tools: `GetRegistrantProfiles`, `GetFairnessBudget`, `ScoreThemeAlignment`, `AppendAuditEntry`
   - Memory: persistent, keyed per event ID
   - **All PII tool calls must route to Foundry Local** — add `[RequiresLocalProvider]` attribute or equivalent routing annotation.
3. Scaffold output models in `src/Bethuya.Core/`:
   - `AttendanceProposal` — list of `ProposedAttendee` with `SelectionReason`
   - `WaitlistProposal` — ordered list with `WaitlistReason`
   - `CurationInsights` — aggregate DEI signals and FairnessBudget summary
4. Create TUnit test stubs in `tests/Bethuya.TUnit.Tests/Agents/`:
   - `WhenCapacityIsExceeded_ProducesAttendanceProposal`
   - `WhenDeiFieldsNotConsented_DoesNotUseForSelection`
   - `NeverAutoRejects_AlwaysReturnsProposal`
   - `EachProposedAttendee_HasHumanReadableSelectionReason`
   - `PiiProcessing_RoutesToFoundryLocal_NotCloudProvider`
5. Verify all hard guardrails are covered by a test.

## Guardrails (always enforce in scaffolded code)
- **All PII processing must route through Foundry Local** (on-device, offline).
- No auto-accept/reject logic — output is always a proposal for human review.
- Every `ProposedAttendee` must include a human-readable `SelectionReason`.
- All organizer overrides must be written to the audit trail via `AppendAuditEntry`.

## Expected Output
- `src/Bethuya.Agents/CuratorAgent.cs` — MAF agent scaffold with TODO markers for business logic
- `src/Bethuya.Core/Models/AttendanceProposal.cs`, `WaitlistProposal.cs`, `CurationInsights.cs`
- `tests/Bethuya.TUnit.Tests/Agents/CuratorAgentTests.cs` — 5 TUnit test stubs (all failing — TDD red phase)
- Summary of what was created and what needs to be implemented next

