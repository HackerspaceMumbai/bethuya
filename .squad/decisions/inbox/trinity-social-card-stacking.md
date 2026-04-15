# 2026-04-15 — Stack asymmetric social verification cards

- **Author:** Trinity
- **Area:** `/registration/social`

## Decision

Stack the LinkedIn and GitHub verification cards vertically on the left column instead of keeping them side-by-side.

## Why

LinkedIn now carries more behavior than GitHub on this step: it owns the extra public-profile URL field and locks that field after verified connect. Keeping both providers in equal-width horizontal cards made the LinkedIn card feel cramped and the GitHub card feel underfilled, even though both states were technically correct.

## Guardrails kept

- LinkedIn URL stays visible in every state.
- Loading and load-error states still disable edits/connect actions until saved state is known.
- GitHub remains the lighter card, but gains supportive copy so the stack still reads as intentional.
