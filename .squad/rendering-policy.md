# Rendering Policy (Web Only)

- Global default: `InteractiveServer` with prerender in `Routes.razor`.
- Per-page overrides:
  - `InteractiveServer` required → auth, PII, organizer/agent-control.
  - `InteractiveWebAssembly` allowed → offline-heavy forms (drafts, multi-step).
- Never place PII flows in WASM.
- Document any override in the PR description with rationale and data classification.

References: Blazor render modes and prerendering.
