# Skill: run-orchestration

## Description
Trigger the event planning workflow (Planner → Scout → Approval) from the CLI. Organizers use this to initiate automated agenda drafting for an event.

## Trigger
Use when you need to draft an event agenda using the Planner agent. Invoke with `/run-orchestration` in Copilot CLI.

## Prerequisites
- Aspire AppHost running (`aspire start`)
- `Hackmum.Bethuya.Backend` project registered as an Aspire resource
- Event already created via the Frontend (status: Draft)

## Parameters
- `--event-id` (required): The GUID of the event to plan
- `--organizer` (required): Email of the organizer (for audit trail and agent context)

## Usage
```bash
copilot /run-orchestration --event-id <guid> --organizer <email@example.com>
```

## Example
```bash
copilot /run-orchestration --event-id 550e8400-e29b-41d4-a716-446655440000 --organizer organizer@hackerspacemumbai.com
```

## What It Does
1. **Validates** the event exists and is in Draft status
2. **Calls** `POST /api/events/{eventId}/orchestrate/plan` with organizer context
3. **Triggers** the Planner agent to:
   - Fetch historical event data from the platform
   - Query Scout agent for speaker availability
   - Draft agenda (title, theme, sessions with time slots)
4. **Polls** the workflow status until draft is created
5. **Returns** the agenda draft URL for human approval

## Expected Output
```
✓ Planner workflow triggered for: GitHub Copilot Dev Days Mumbai
✓ Theme: "Open Source Contributions" (confidence: 92%)
✓ 5 sessions planned over 3 hours
✓ Agent reasoning: Agenda optimized for 4-hour venue slot with 3 confirmed speakers
→ Review and approve: https://localhost:7112/approvals/plan/550e8400-e29b-41d4-a716-446655440000
```

## Error Scenarios
- **Event not found** — Verify event ID and that the event is in Draft status
- **Backend not running** — Start Aspire AppHost: `aspire start`
- **Planner timeout** — Check Aspire logs for agent failures; see `aspire logs planner-agent`

## Related Skills
- `/explain-diff` — Review approval edits before merging
- `/seed-db` — Create test events for planning workflows

## Notes
- Planner uses Foundry Local for privacy-sensitive event analysis
- Speaker availability queries are stateless (no local caching)
- Approvals must happen via the Frontend approval UI (`/approvals/plan/{eventId}`)
- This skill is **read-only** for event data; it does not modify the event itself
