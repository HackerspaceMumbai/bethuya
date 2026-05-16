# Bethuya Multi-Agent Workflow — UX Manual Testing Guide

**Date:** May 2026  
**Purpose:** Step-by-step browser-based validation of the complete event planning → curation → approval → reporting workflow  
**Audience:** QA, organizers, stakeholders verifying agent-assisted event management

---

## 🚀 Quick Start

### Prerequisites

- Aspire running: `aspire start --isolated`
- Backend healthy: `https://localhost:7400/health` responds 200
- Web UI accessible: `https://localhost:7400`
- Logged in as **Organizer** or **Admin** role

### Dashboard Access

1. Open `https://localhost:7400`
2. Log in (auth branch determines method: Entra, Auth0, Keycloak)
3. You should see: **Dashboard** → **Events** → **Organizer Tools** (if authorized)

---

## 📋 Complete UX Workflow: Event → Plan → Curate → Approve → Report

### Phase 1: Event Creation & Navigation

#### Step 1.1: Navigate to Events List

- Click **Events** in left sidebar
- Expected: See event list (may be empty for first run)
- **Data-test selector:** `nav-events-link`

#### Step 1.1b: About Organizer Tools in Navigation

- The left sidebar shows an **"Organizer Tools"** section (if you have Organizer or Curator role)
- This section now displays: "ℹ️ Select an event to access agent workflows and curation. Agent features are accessed from event detail pages."
- **Why:** The Agent Workflows and Attendee Curation features require a specific event ID. They are accessed from **within an event detail page**, not from the sidebar.
- **Next:** Create/click an event to reveal the agent workflow buttons on the event detail page.

#### Step 1.2: Create New Event

- Click **"➕ Create New Event"** button (or similar CTA)
- Form appears with fields:
  - Title: `"AI Community Meetup - May 2026"`
  - Description: `"Discussing AI agents, prompt engineering, and responsible development"`
  - Type: Select `"Meetup"`
  - Capacity: `50`
  - Start Date/Time: `May 20, 2026 at 14:00 IST`
  - End Date/Time: `May 20, 2026 at 16:00 IST`
  - Location: `"HackerSpace Mumbai, Bandra"`
  - Hashtag: `#ai-meetup-may-2026`
- **Screenshot:** Verify all fields match (capture for audit trail)
- Click **"✅ Create Event"**
- Expected: Redirect to **Event Detail** page; display event card with status badge

#### Step 1.3: Verify Event Created

- Page shows:
  - Event title, date, time, capacity
  - Status badge (should be `Draft`)
  - Tabs: **Overview**, **Registrations**, **Agent Workflows**, **Approvals**
  - **"🤖 Draft Schedule with AI"** button (Planner entry point)
  - **"📊 Curation"** button (Curator entry point, if authorized)
- **Data-test selector:** `event-detail-title`

---

### Phase 2: Attendee Registration (Setup for Curation)

#### Step 2.1: Add Test Registrations

- Click **Registrations** tab
- Click **"➕ Add Registration"** or similar
- For each attendee (create 15–20):
  - Name: `"Test Attendee {N}"`
  - Email: `"attendee{N}@test.local"`
  - Bio: `"Interested in AI, community building"`
  - Interests: Select `["AI", "Machine Learning", "Community"]`
  - Optional DEI fields (if present):
    - Gender: Some "Woman", some "Man", some "Other"
    - First-time?: Mix of yes/no
    - Background: Mix of values
- **Screenshot:** Verify all registrations appear in list
- Expected: Registrations count increases; capacity warning if > capacity

#### Step 2.2: Verify Registrations

- Registrations tab shows table with columns: Name, Email, Interests, Status, Actions
- At least 15 attendees visible (scroll if needed)
- **Data-test selector:** `registrations-list`, `registration-row-{N}`

---

### Phase 3: Planner Agent — Draft Agenda

#### Step 3.1: Open Event Detail & Planner Section

- From Events list, click the event you created
- Scroll to **"🤖 Agent Workflows"** or similar section
- See button: **"🤖 Draft Schedule with AI"**
- **Data-test selector:** `btn-draft-with-planner`

#### Step 3.2: Invoke Planner Agent

- Click **"🤖 Draft Schedule with AI"**
- Modal/panel appears with:
  - Input field(s) for constraints (optional)
  - Example: `"90 minutes, 3 speakers, focus on enterprise AI"`
  - **"Generate Draft"** button
- Enter a constraint or leave blank (agent uses event context)
- Click **"Generate Draft"**
- **Status indicator:** Shows "⏳ Generating..." (may take 5–10 seconds)

#### Step 3.3: View Hybrid Markdown Agenda

- After generation, agenda appears in **markdown editor**:
  - Top-level heading with event title, date, timezone
  - Timeline/table with blocks: time, title, description
  - Sections: Objectives, Constraints, Risks, Next Actions
  - All human-readable; suitable for editing
- **Screenshot:** Capture markdown view
- **Data-test selector:** `planner-markdown-editor`

#### Step 3.4: Toggle to JSON Schema Tab

- Look for tabs at top of agenda viewer: **📝 Markdown** | **📋 Schema**
- Click **📋 Schema** tab
- Expected: Same agenda, but in **structured JSON format**:
  - `agendaVersion`: "1.0"
  - `event`: { eventId, title, date, timezone, location }
  - `agenda`: { totalDurationMinutes, blocks: [] }
  - `rationale`: { keyTradeoffs, inclusionNotes }
  - `risks`: { items }
  - `nextActions`: { items }
- **Screenshot:** Capture JSON structure (verify consistency with Markdown)
- **Data-test selector:** `planner-json-tab`, `planner-schema-viewer`

#### Step 3.5: Edit Markdown (Optional)

- Click back to **📝 Markdown** tab
- Edit a block title or add a time note (e.g., add "Q&A" block)
- Expected: The markdown editor is directly editable; there is no separate **Edit** or **Apply Changes** button in the current UI
- After editing, click **✅ Approve** to validate and reconcile the draft before publishing
- Expected: Markdown and JSON both update when the draft is approved
- **Screenshot:** Show edited markdown
- **Data-test selector:** `planner-markdown-editor`, `approve-btn`

#### Step 3.6: Approve Draft

- Below agenda, click **"✅ Approve"** button
- Modal asks: "Ready to approve? This moves to 'Ready to Publish'."
- Click **"Confirm"**
- Expected:
  - Status changes to `ReadyToPublish`
  - Button text changes to **"📢 Publish Schedule"**
  - Planner buttons disabled (cycle locked once published)
- **Screenshot:** Show status transition
- **Data-test selector:** `btn-approve-draft`, `planner-status-badge`

#### Step 3.7: Publish Schedule (Finalize)

- Click **"📢 Publish Schedule"**
- Modal: "Publish to final schedule? This will lock the cycle and block refinement."
- Click **"Confirm"**
- Expected:
  - Status: `Published` (immutable snapshot stored)
  - Button text: **"🔁 Start New Cycle"** (if user wants to make post-publish changes)
  - Message: ✅ "Schedule published. This cycle is now closed."
  - Audit entry recorded with markdown + JSON snapshot
- **Screenshot:** Show published state
- **Data-test selector:** `btn-publish-schedule`, `cycle-status-published`

---

### Phase 4: Curator Agent — Attendee Curation

#### Step 4.1: Navigate to Curation View

- From Event Detail page, click **"📊 Attendee Curation"** button
- Expected: Redirect to `/curation/{eventId}` page (direct route, event context required)
- ⚠️ Warning banner: "⚠️ This view handles attendee PII - processed locally via Foundry Local. Never sent to cloud."
- **Data-test selector:** `btn-open-curation`, `curation-pii-warning`

#### Step 4.2: Review Fairness Budget (Diversity Targets)

- Left panel shows: **Fairness Budget**
- Fields for aspirational targets:
  - Women: 35%
  - Minorities: 30%
  - First-time attendees: 40%
  - Edit sliders or input fields
- Click **"Apply Targets"** or auto-apply
- Expected: UI shows progress bars updating
- **Screenshot:** Capture fairness budget UI
- **Data-test selector:** `fairness-budget-section`, `target-slider-women`, `target-slider-minorities`

#### Step 4.3: Invoke Curator Agent

- Button: **"🤖 Curate Attendees"** or **"Run Curation"**
- Click to invoke
- **Status indicator:** Shows "⏳ Processing..." (5–15 seconds, as Foundry Local runs locally)
- Expected: **No cloud calls** (verify in browser Network tab: no requests to openai.com, openai.azure.com, etc.)

#### Step 4.4: View Curation Proposal

- After completion, results panel shows:
  - **Attendance Proposal:**
    - ✅ Selected count: e.g., "50 attendees selected"
    - Fairness metrics:
      - Women: 35% (met target)
      - Minorities: 28% (close to 30%)
      - First-timers: 42% (exceeded 40%)
  - **Waitlist Proposal:** e.g., "10 attendees on waitlist"
  - **Fairness Score:** e.g., "0.92 / 1.0"
  - **Curation Insights:** Human-readable explanation (e.g., "Selected for diversity + community balance")
- ⚠️ **NO attendee names/emails shown** — only aggregate stats
- **Screenshot:** Capture curation results
- **Data-test selector:** `curation-results`, `fairness-score`, `insights-text`

#### Step 4.5: Approve Curation (Create Approval Record)

- Button: **"✅ Approve Curation"**
- Modal:
  - Label: "Approve this curation proposal?"
  - Input: Reason/notes (optional)
  - Example: "Fairness metrics look good, diversity goals met"
- Click **"Confirm"**
- Expected:
  - Status: `ApprovedByHuman`
  - Audit record created (timestamp, approver, reason, fairness snapshot)
  - Success banner: "✅ Curation approved"
  - PII deleted from memory (background cleanup)
- **Screenshot:** Show approval confirmation
- **Data-test selector:** `btn-approve-curation`, `approval-modal`

---

### Phase 5: Approvals Workflow (Human Review)

#### Step 5.1: Navigate to Approvals Tab

- From Event Detail, click **Approvals** tab
- Expected: List of all decisions for this event:
  - Planner drafts (if any pending)
  - Curation approvals (should show the one you just approved)
  - Reporter summaries (if any)
- **Data-test selector:** `approvals-tab`, `approvals-list`

#### Step 5.2: View Approval Details

- Click on a curation approval record
- Details modal shows:
  - Entity type: `Curator`
  - Status: `ApprovedByHuman`
  - Timestamp: When approved
  - Approver: Your user ID
  - Reason: Your notes
  - Fairness snapshot: Full JSON of the proposal at approval time
  - Audit trail: Input hash, agent version, trace ID
- **Screenshot:** Capture approval record
- **Data-test selector:** `approval-record-{id}`, `approval-details-modal`

---

### Phase 6: Reporter Agent — Post-Event Summary (Optional Future Workflow)

#### Step 6.1: (Future) Invoke Reporter Agent

- *This is a forward-looking placeholder for post-event workflows*
- After event completes, **Reporter Agent** would:
  1. Read event notes (if any)
  2. Summarize key discussion points
  3. Extract action items
  4. Generate attendee feedback if available
  5. Produce HTML/PDF summary
- Expected workflow (when implemented):
  - Click **"📄 Generate Post-Event Report"**
  - Select: "Include attendee feedback?" → Yes/No
  - Reporter produces Markdown + JSON hybrid output (like Planner)
  - Human edits + approves
  - Report published to event page
- **Data-test selector:** (TBD when implemented) `btn-generate-report`

---

## 🔐 Security & Privacy Verification

### Verify PII Stays Local (Curator)

#### Network Isolation Check

1. Open browser **DevTools → Network tab**
2. Filter: Show only XHR/Fetch requests
3. Navigate to Curation view
4. Click **"🤖 Curate Attendees"**
5. Expected: Only requests to `localhost` or internal Azure services
6. **❌ FAIL if:** Any requests to `openai.com`, `openai.azure.com`, `api.anthropic.com`, etc.
7. **Screenshot:** Capture Network tab showing only local calls

#### Renderer Mode Verification

1. Open **DevTools → Elements tab**
2. Search for `<Curation...>` component in HTML
3. Check `@rendermode` — should be **`InteractiveServer`** (server-side, not WASM)
4. **Why:** Client-side WASM can be inspected; server-side ensures PII never reaches client
5. **Screenshot:** Show `@rendermode InteractiveServer` in source

### Verify Approval Audit Trail

1. Approvals tab → Click a curation approval
2. Details should include:
   - ✅ Input hash (SHA256)
   - ✅ Approver timestamp
   - ✅ Agent version tag
   - ✅ TraceParent / CorrelationId (for distributed tracing)
   - ✅ Fairness snapshot (full JSON preserved)
3. **Screenshot:** Capture complete audit record

---

## 📊 Test Data Reference

### Sample Event

```
Title:       AI Community Meetup - May 2026
Type:        Meetup
Capacity:    50
Start:       2026-05-20 14:00 IST
End:         2026-05-20 16:00 IST
Location:    HackerSpace Mumbai, Bandra
Hashtag:     #ai-meetup-may-2026
```

### Sample Registrations (Create 15–20)

```
Attendee 1: Name="Alice", Email="alice@test.local", Gender="Woman", FirstTime=Yes
Attendee 2: Name="Bob", Email="bob@test.local", Gender="Man", FirstTime=No
Attendee 3: Name="Carol", Email="carol@test.local", Gender="Woman", FirstTime=Yes
... (repeat with variation)
```

### Sample Fairness Budget

```
Women: 35%
Minorities: 30%
First-time Attendees: 40%
```

---

## ✅ Acceptance Checklist

- [ ] **Event Creation:** Event created with all fields populated correctly
- [ ] **Registrations:** 15+ attendees registered; list visible
- [ ] **Planner Markdown:** Agenda renders with readable schedule + sections
- [ ] **Planner JSON:** Schema tab shows valid JSON matching Markdown
- [ ] **Planner Edit:** Can edit Markdown; JSON updates automatically
- [ ] **Planner Approval:** Draft approved; status moves to `ReadyToPublish`
- [ ] **Planner Publish:** Schedule published; cycle locked; "Start New Cycle" available
- [ ] **Curation Fairness Budget:** Targets set; sliders functional
- [ ] **Curator Invocation:** Runs locally; completes in < 15 seconds
- [ ] **Curation Results:** Fairness scores + insights shown; NO PII visible
- [ ] **Curation Approval:** Approved; audit record created
- [ ] **Approvals Tab:** All decisions visible with full details
- [ ] **Network Isolation:** Only localhost calls during Curation (no cloud PII egress)
- [ ] **Audit Trail:** Each approval has hash, timestamp, approver, fairness snapshot
- [ ] **Error Handling:** Invalid inputs show clear error messages
- [ ] **Screenshots:** All UI states captured for documentation

---

## 🛠️ Troubleshooting

### Nav Links Appear Broken

**Issue:** Sidebar shows "Agent Workflows" and "Attendee Curation" but clicking does nothing.

**Cause:** These routes require a `{EventId}` parameter (e.g., `/agents/{eventId}`). The nav links should either:

- Only show when viewing an event detail page, OR
- Navigate to the event list first

**Workaround:**

1. Go to **Events** list
2. Click an event to open **Event Detail**
3. From Event Detail, click **"🤖 Agent Workflows"** button (CTA within page)
4. OR use URL directly: `https://localhost:7400/agents/{YOUR_EVENT_GUID}`

**Fix:** (See next section)

### Page Load Fails (404)

**Issue:** Navigating to `/agents/{eventId}` shows 404 error.

**Causes:**

- Backend not running → Check `https://localhost:7400/health`
- Aspire resources failed to start → Run `aspire stop`, then `aspire start --isolated`
- Database not seeded → You may need to create an event first via API

### Curator Shows Error: "Not Authorized"

**Issue:** Clicking "Attendee Curation" shows "You are not authorized to view this resource."

**Cause:** Your user doesn't have the `Curator` role.

**Fix:**

- Auth branches (feature/auth/entra, feature/auth/auth0, feature/auth/keycloak) define role assignment
- In dev, you may need to seed your user with the `Curator` role
- Check `.squad/` or auth branch configuration for role assignment logic

### Network Shows Cloud API Calls During Curation

**Issue:** DevTools Network tab shows requests to `api.openai.com` while running Curator.

**Cause:** ❌ **CRITICAL SECURITY ISSUE** — PII is being sent to cloud.

**Fix:**

- Verify FoundryLocal is running: `curl http://localhost:5272/health`
- Check backend logs for "Using Foundry Local" message
- If Foundry Local crashed, restart it: See `docs/AI_PROVIDER_REFACTORING.md`
- Escalate to dev team

---

## 🔄 Test Execution Log Template

Use this template to record your UX testing session:

```markdown
## UX Test Session — [DATE] [TESTER_NAME]

### Environment
- Aspire Start Time: [TIME]
- Backend Health: [200 OK / Error]
- Web UI Accessible: [Yes / No]
- Logged-in User Role: [Admin / Organizer / Curator]

### Test Results

#### Phase 1: Event Creation
- Event created: [✅ / ❌]
- All fields populated: [✅ / ❌]
- Redirect to detail page: [✅ / ❌]
- Screenshots: [Saved to: ___]

#### Phase 2: Registrations
- 15+ registrations added: [✅ / ❌]
- List displays correctly: [✅ / ❌]
- Screenshots: [Saved to: ___]

#### Phase 3: Planner
- Markdown agenda generated: [✅ / ❌]
- JSON schema valid: [✅ / ❌]
- Tab toggle works: [✅ / ❌]
- Approve/Publish workflow: [✅ / ❌]
- Screenshots: [Saved to: ___]

#### Phase 4: Curator
- Curation runs without error: [✅ / ❌]
- Fairness scores calculated: [✅ / ❌]
- No PII visible in results: [✅ / ❌]
- Network isolation verified: [✅ / ❌]
- Approval recorded: [✅ / ❌]
- Screenshots: [Saved to: ___]

#### Phase 5: Approvals
- All records visible: [✅ / ❌]
- Audit trail complete: [✅ / ❌]
- Screenshots: [Saved to: ___]

### Issues Found
1. [Issue description] — [Severity: Critical / High / Medium / Low]
2. [Issue description] — [Severity: Critical / High / Medium / Low]

### Notes
[Any additional observations, environment quirks, or follow-up actions]
```

---

## 📞 Questions & Support

If you encounter issues during UX testing:

1. Check **Network tab** for cloud API calls (PII violation indicator)
2. Review **Browser console** for JavaScript errors
3. Check **Aspire Dashboard logs** for backend errors
4. Consult `docs/API_TESTING_GUIDE_ACCURATE.md` for API-level testing
5. See `AGENTS.md` for architecture & cycle semantics

---

**Last Updated:** May 2026  
**Status:** ✅ D1–D6 complete and tested  
**Next Phase:** Option B A2A delegation, other agents (Facilitator, full Reporter)
