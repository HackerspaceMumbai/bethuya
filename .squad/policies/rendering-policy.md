# Rendering Policy (Web Only)

- Global default: `InteractiveServer` configured app-wide in `App.razor` (on `Routes` and `HeadOutlet`), ensuring consistent Auth0 authentication and Aspire service-discovery behavior across all pages.
- Per-page overrides:
  - `InteractiveServer` required → auth, PII, organizer/agent-control.
  - `InteractiveWebAssembly` allowed → offline-heavy forms (drafts, multi-step) **only when the flow contains no external OAuth redirects**. If the form can trigger an OAuth redirect (e.g., Auth0 login), the Blazor circuit is destroyed during the redirect and all WASM in-memory and browser-storage state may be lost. In those cases, **save draft data server-side** (e.g., via `IRegistrationDraftService`) before initiating the redirect.
- Never place PII flows in WASM.
- Document any override in the PR description with rationale and data classification.

References: Blazor render modes and prerendering.
