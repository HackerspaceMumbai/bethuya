# 2026-04-15 — Gate LinkedIn connect on a real URL and signal the stacked GitHub card

## Context

`/registration/social` already kept the LinkedIn URL field visible and locked it after verification, but the connect action could still launch with a blank URL. In the stacked-card layout, some users also missed that GitHub continued below the larger LinkedIn card.

## Decision

- Disable the LinkedIn connect/reconnect CTA until the current unverified state has a non-empty trimmed public LinkedIn profile URL.
- Keep the verified LinkedIn lock state unchanged so the member-ID completion boundary still comes from LinkedIn, not from typed text.
- Add a compact stack-intro cue above the cards that explicitly tells users GitHub verification continues below LinkedIn.

## Why

This removes the normal blank-url path that could lead to a locked empty LinkedIn URL after verification, without weakening the verified-member-ID rule. The intro cue improves scanability in the stacked layout without adding noisy chrome or pushing sensitive logic into client-only behavior.
