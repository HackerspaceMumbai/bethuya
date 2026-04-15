# Trinity — Profile edit entry routing

- **Date:** 2026-04-15
- **Area:** Profile navigation / onboarding edit flow
- **Decision:** Route dashboard `Profile` clicks through a dedicated `/profile` resolver instead of linking directly to `/registration/mandatory`.
- **Why:** The dashboard needs one stable entrypoint that can resume incomplete users at the required step while still letting completed users re-enter the saved edit flow without implying blank state before hydration.
- **Impact:** Main nav now points at `/profile`; the resolver sends incomplete users to mandatory/social as before and sends completed users into the mandatory edit step, where saved mandatory/social/AIDE data hydrates with explicit loading states.
