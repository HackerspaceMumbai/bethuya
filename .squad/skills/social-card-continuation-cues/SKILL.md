---
name: "social-card-continuation-cues"
description: "Keep stacked onboarding cards state-stable while making the next card visually obvious."
domain: "frontend"
confidence: "high"
source: "earned"
tools:
  - name: "playwright-browser_take_screenshot"
    description: "Capture visual proof after the live page has been rebuilt."
    when: "When a card-flow or continuation cue changes visually."
---

## Context

Use this skill when a multi-step or stacked card flow becomes asymmetric but card order must remain fixed for trust, memory, or regression stability.

## Patterns

- Keep provider or step order stable across disconnected, loading, mixed, and connected states.
- Add a compact flow map near the top of the stack so users understand the fixed order before they scroll.
- Add an inter-card bridge between the current card and the next card so the continuation cue stays visible when the first card becomes taller.
- Intensify the bridge or follow-up card accent only when the first card is complete and the second card is still pending.
- Expose `data-test` markers for the cue structure (`social-stack-path`, `github-stack-bridge`) so UI regressions can be caught without relying on pixel comparisons.

## Examples

- `src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Pages\SocialProfileConnections.razor`
- `src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Pages\SocialProfileConnections.razor.css`
- `tests\Hackmum.Bethuya.Tests\UI\OnboardingNavigationRenderTests.cs`

## Anti-Patterns

- Reordering cards based on completion state.
- Using only explanatory text at the top of the page when the real confusion happens after the first card grows.
- Making the continuation cue so loud that the default disconnected state looks like an error or warning.
