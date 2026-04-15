---
updated_at: 2026-04-15T05:56:27.209Z
focus_area: Complete LinkedIn URL onboarding UX with state-stable social cards
active_issues:
  - "Finish Trinity's `/registration/social` UI work so the LinkedIn public profile URL field is editable before verification, locks after verified connect, and keeps the GitHub/LinkedIn cards aligned."
completed_security_work:
  - "2026-04-11: Hardened nav role visibility during onboarding (AuthorizeView on AI Agents/Curation)"
  - "2026-04-11: Added explicit InteractiveServer render mode to Home.razor"
---

# What We're Focused On

LinkedIn social onboarding URL UX is the active focus. The remaining work is frontend-only polish and validation on `/registration/social`: keep the UI state-stable across disconnected, connected, mixed, and error states while preserving the verified-member-ID requirement for LinkedIn completion.
