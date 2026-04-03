# Rendering policy (Web only)

## Defaults

- Global default: `InteractiveServer` configured app-wide (typically via
  `Routes.razor` and/or `App.razor` depending on template).

## Per-page overrides

- `InteractiveServer` required:
  - auth
  - PII
  - organizer/admin
  - agent-control surfaces
- `InteractiveWebAssembly` allowed:
  - offline-heavy forms (drafts, multi-step)
  - only when the flow contains no external OAuth redirect mid-flow

## Redirect safety rule

If a form flow can trigger an external OAuth redirect (for example, Auth0 login),
persist draft state before the redirect and restore it after return. Do not rely
on in-memory state.

## Additional rules

- Never place PII flows in WASM.
- Document any override in the PR description with rationale and data
  classification.