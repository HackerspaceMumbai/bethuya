# **🏛️ Bethuya — The Agentic Community Event Intelligence Platform**

> *HackerspaceMumbai • Debuting at GitHub Copilot Dev Days • Built initially for AI Dev Days • Maintained for the Community*

**Bethuya** is an **AI‑augmented, Agent-first, .NET-built,  Aspire–orchestrated** platform for planning, curating, running, and reporting community events.

> **Principle:** AI **drafts**, humans **approve**, community **owns**.\
> **Reality:** Demo‑ready today, **backbone of HackerspaceMumbai** tomorrow.

***

## ✨ What Bethuya Does

* **Plan** — *Planner Agent* drafts agendas, timings, and speaker suggestions.
* **Curate (Attendees)** — *Curator Agent* helps select attendees fairly when registrations exceed capacity (often **3×**), balancing **theme suitability** and **DEI**; outputs are explainable **recommendations**, never auto‑rejections.
* **Run** — *Facilitator Agent* suggests prompts, Q\&A, and captures notes (organizer‑controlled).
* **Report** — *Reporter Agent* drafts summaries, highlights, and action items.
* **Approve** — **Human‑in‑the‑loop** diffs, approvals, and a full audit trail.

***

## 🧱 Technology Stack

* **.NET 10** + **C# 14**
* **.NET Aspire** - orchestration, composition, service discovery, config/secrets, and the **Aspire Dashboard** for logs, traces, and health. [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/dotnet/aspire/), [\[aspire.dev\]](https://aspire.dev/dashboard/explore/)
* **Blazor Hybrid (.NET MAUI)** - one UI targeting Android, iOS, macOS, Windows via WebView; can also share UI with Blazor Web App. [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0), [\[learn.microsoft.com\]](https://learn.microsoft
* **API Documentation:** **Scalar** - integration for all endpoints..com/en-us/aspnet/core/blazor/hybrid/tutorials/maui?view=aspnetcore-10.0)
* **Blazor Blueprint UI** — modern, accessible Blazor component library with pre-built styles, headless primitives, and shadcn/ui theme compatibility. No Tailwind or Node.js required. [\[blazorblueprintui.com\]](https://blazorblueprintui.com/)
* **AI Providers (routed)** - **Foundry Local** (sensitive/offline), **Ollama** (local), **Azure OpenAI** / **OpenAI** (non‑sensitive/public). Foundry Local provides an OpenAI‑compatible local runtime on Windows/macOS. [\[devblogs.m...rosoft.com\]](https://devblogs.microsoft.com/foundry/unlock-instant-on-device-ai-with-foundry-local/), [\[github.com\]](https://github.com/microsoft/Foundry-Local)
* **Agents** - **Microsoft Agent Framework** for tool‑calling, memory, and multi‑agent workflows; works with .NET and Python. [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/agent-framework/), [\[github.com\]](https://github.com/microsoft/agent-framework)
* **Dev AI** - **GitHub Copilot SDK** (repo‑aware skills & sessions), **Copilot CLI** (agentic terminal workflows; GA), **VS Code Insiders**. [\[github.com\]](https://github.com/github/copilot-sdk/blob/main/docs/guides/skills.md), [\[github.blog\]](https://github.blog/changelog/2026-02-25-github-copilot-cli-is-now-generally-available/)
* **Testing** - **TUnit** for unit/integration tests and **Playwright for .NET** for E2E tests (Chromium/WebKit/Firefox) with tracing. [\[TUnit\]](https://github.com/tunit-framework/TUnit) [\[playwright.dev\]](https://playwright.dev/dotnet/docs/intro)

***

## 🎨 Frontend Architecture — **Blazor Hybrid + Blazor Blueprint UI**

* **Blazor Blueprint UI** provides 80+ accessible, pre-styled components and 17 headless primitives for Blazor. No Tailwind, DaisyUI, or Node.js required.
* **Theme compatibility:** Use any [shadcn/ui](https://ui.shadcn.com/themes) or [tweakcn.com](https://tweakcn.com/) theme by copying CSS variables into your `theme.css`.
* **Dark mode:** Built-in, just add `.dark` to `<html>`.
* **Setup:**

```bash
# Add Blazor Blueprint UI to your project
dotnet add package BlazorBlueprint.Components
# Optionally add icon libraries
dotnet add package BlazorBlueprint.Icons.Lucide
```

**Minimal setup:**

1. Register services in `Program.cs`:

  ```csharp
  builder.Services.AddBlazorBlueprintComponents();
  ```

2. Add to `_Imports.razor`:

  ```razor
  @using BlazorBlueprint.Components
  ```

3. Reference styles in your host page (e.g., `wwwroot/index.html`):

  ```html
  <!-- Optional: your theme variables -->
  <link rel="stylesheet" href="styles/theme.css" />
  <!-- Blazor Blueprint styles -->
  <link rel="stylesheet" href="_content/BlazorBlueprint.Components/blazorblueprint.css" />
  ```

4. Add `<BbPortalHost />` to your root layout for overlays:

  ```razor
  <BbPortalHost />
  ```

5. Use components:

  ```razor
  <BbButton Variant="ButtonVariant.Default">Click me</BbButton>
  ```

See [blazorblueprintui.com](https://blazorblueprintui.com/) for full docs and component gallery.

> **Testing tip:** Prefer `data-test` selectors in Razor components to make Playwright tests resilient to class changes. [\[playwright.dev\]](https://playwright.dev/dotnet/docs/intro)

***

### 🌟 Hackathon "Hero" Technology Implementation

#### Microsoft Foundry & Foundry Local (The Privacy Hero)

* Foundry Local: Processes sensitive PII (registrant bios/profiles) locally to ensure DPDP compliance.

* Azure AI Foundry: Manages high-reasoning agent lifecycles and global orchestration via the azure-ai-projects SDK.

#### Microsoft Agent Framework (Orchestration Hero)

Implements specialized agents like @curator with persistent memory and hosted tools.

#### NET Aspire 10 (Platform Hero)

Acts as the connective tissue for local orchestration (Azure SQL, Redis) and cloud deployment.

#### GitHub Copilot & agents.md (DX Hero)

Custom agent personas in .github/agents/ enforce coding standards and project context.

#### Blazor Blueprint UI

Accessible, headless primitives for a high-performance shadcn/ui inspired frontend.

## 📊 Performance & Verification

| Metric | Target | Verification Tool |
| :--- | :--- | :--- |
| **Hot Path Latency (p99)** | < 180ms @ 2,500 RPS | [cite_start]NBomber / TUnit [cite: 849, 876] |
| **Memory Allocation** | 0 B on Hot Path | [cite_start]BenchmarkDotNet [cite: 484, 536] |
| **Visual Accuracy** | 100% Match | [cite_start]Playwright Visual Regression [cite: 890, 894] |

## 🔄 Development Protocol

- **Plan First**: All tasks must be planned in `tasks/todo.md` before execution[cite: 881, 886].
* **No Laziness**: Identify root causes; no temporary hacks[cite: 888].
* **Autonomous Fixes**: Agents are authorized to resolve failing tests/CI without manual intervention[cite: 885].

## 📁 Repository Structure (Aspire + Frontend + Dev‑AI)

    / (root)
    ├─ Bethuya.slnx
    ├─ Directory.Build.props
    ├─ README.md
    │
    ├─ src/
    │  ├─ Hackmum.Bethuya.App/              # MAUI Blazor Hybrid client (WebView, Blazor Blueprint UI in wwwroot)
    │  │   ├─ wwwroot/
    │  │   │   ├─ theme.css                 # (optional) shadcn/tweakcn theme variables
    │  │   │   └─ index.html                # host page, references blazorblueprint.css
    │  ├─ Hackmum.Bethuya.Core/             # Domain: Events, Registrations, Decisions, FairnessBudget
    │  ├─ Hackmum.Bethuya.Agents/           # Planner, Curator(attendees), Facilitator, Reporter
    │  ├─ Hackmum.Bethuya.AI/               # Provider router (Foundry/Ollama/Azure/OpenAI), prompts, memory
    │  ├─ Hackmum.Bethuya.Backend/          # Backend APIs/services (Aspire-connected)
    │  ├─ Hackmum.Bethuya.Infrastructure/   # Storage (Azure SQL), repos, platform adapters
    │  └─ Hackmum.Bethuya.Tests/            # Unit & integration tests (TDD)
    │
    ├─ tests/
    │  └─ Hackmum.Bethuya.E2E/              # Playwright .NET E2E (with traces)
    │
    ├─ aspire/
    │  ├─ Hackmum.Bethuya.AppHost/          # Aspire AppHost (compose Backend + Workers + Resources)
    │  ├─ Hackmum.Bethuya.ServiceHost/      # Agent workers + resources
    │  ├─ resources/                        # DB / queue config; migrations; seeders
    │  └─ dashboards/                       # Observability presets
    │
    ├─ copilot/
    │  ├─ skills/                           # GitHub Copilot SDK skills (repo-aware)
    │  └─ extension/                        # Optional VS Code extension wiring
    │
    └─ assets/                               # Branding, screenshots, sample data

***

## 🧠 Agents (Human‑in‑the‑Loop Contracts)

### Planner Agent — Agenda Drafting

Inputs: prior events, interests, constraints → **draft agenda** (sessions, timings, suggested speakers).\
**UX:** Approve / Edit / Reject (diffs + audit).

### **Curator Agent — Responsible Attendee Curation (Oversubscription)**

We frequently see **3× registrations** vs capacity. The agent assists humans to build an **open, diverse, inclusive, equitable, and theme‑aligned** attendee list.

* **Provides:** theme alignment signals (self‑reported data), community continuity, **DEI nudges** (consented fields only), equity prompts, first‑come signals (when applicable), over‑representation alerts.
* **Never:** auto‑accepts/rejects, infers sensitive traits, hides reasoning, uses opaque scoring.
* **Outputs:** **AttendanceProposal**, **WaitlistProposal**, **CurationInsights**, **FairnessBudget** targets → humans decide.

### Facilitator Agent — Live Assistance (Opt‑in)

Prompts, Q\&A suggestions, live notes (organizer‑controlled publish).

### Reporter Agent — Post‑Event Drafting

Summary, highlights, action items → human edits → publish (attribution).

> **Agent runtime & tools:** implemented using **Microsoft Agent Framework** for tool‑calling, memory, and multi‑agent workflows in .NET. [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/agent-framework/), [\[github.com\]](https://github.com/microsoft/agent-framework)

***

## 🧩 Architecture with **.NET Aspire**

**Aspire AppHost** composes the distributed app for local development with one command, wiring up services, storage, and the **Aspire Dashboard** for observability (logs, traces, health). [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/dotnet/aspire/), [\[aspire.dev\]](https://aspire.dev/dashboard/explore/)

``` folder structure
    AppHost
      ├─ Backend API (Hackmum.Bethuya.Backend)
      ├─ Agent Workers
      │    ├─ PlannerWorker
      │    ├─ CuratorWorker
      │    └─ ReporterWorker
      ├─ Storage
      │    └─ Azure SQL
      ├─ Queue (optional) for async agent jobs
      └─ Observability (Aspire Dashboard)
```

***

## 🔌 Provider Routing (Privacy‑aware)

We route AI calls by sensitivity:

1. **Foundry Local** — default for attendee curation/sensitive data (on‑device, OpenAI‑compatible API; Windows/macOS). [\[devblogs.m...rosoft.com\]](https://devblogs.microsoft.com/foundry/unlock-instant-on-device-ai-with-foundry-local/), [\[github.com\]](https://github.com/microsoft/Foundry-Local)
2. **Ollama** — local LLMs.
3. **Azure OpenAI** — enterprise boundary for non‑sensitive/public drafts.
4. **OpenAI** — optional, non‑sensitive public content.

> **Foundry Local** chooses optimized model variants for your hardware and runs fully offline once models are cached. [\[github.com\]](https://github.com/microsoft/Foundry-Local), [\[clemenssiebler.com\]](https://clemenssiebler.com/posts/running-slm-locally-azure-foundry-local/)

***

## 🎤 Debuting at **GitHub Copilot Dev Days** — AI‑Assisted Development

Bethuya showcases state‑of‑the‑art developer AI:

* **GitHub Copilot SDK** — repo‑aware **skills** loaded into Copilot sessions (add skills under `copilot/skills/**/SKILL.md`). [\[github.com\]](https://github.com/github/copilot-sdk/blob/main/docs/guides/skills.md)
* **Agent Skills** in VS Code — portable, on‑demand capabilities that load automatically when relevant. [\[code.visua...studio.com\]](https://code.visualstudio.com/docs/copilot/customization/agent-skills), [\[github.blog\]](https://github.blog/changelog/2025-12-18-github-copilot-now-supports-agent-skills/)
* **Copilot CLI (GA)** — terminal‑native **agent** for plan/review/edit/run with parallelized sub‑agents and session memory. [\[github.blog\]](https://github.blog/changelog/2026-02-25-github-copilot-cli-is-now-generally-available/)

**Example skills** we ship:

* `/seed-db` — seed dev data via Backend API.
* `/curate-attendees` — scaffold Curator pipelines & tests.
* `/run-e2e` — run Playwright and summarize failures w/ traces.
* `/explain-diff` — PR summaries & risk callouts.
* `run-benchmarks` — Verify **0B allocation** targets via BenchmarkDotNet

> The Copilot CLI is GA and supports agentic workflows (plan/autopilot), deep repo & PR integration, and skills/MCP extensibility. [\[github.blog\]](https://github.blog/changelog/2026-02-25-github-copilot-cli-is-now-generally-available/), [\[github.com\]](https://github.com/features/copilot/cli/)

***

## 🧪 Verification driven AI‑Assisted **TDD + Playwright** feedback Loop

* **Unit/Domain tests** (TDD first): `dotnet watch test src/Hackmum.Bethuya.Tests` (using **TUnit**). Every feature begins with a TUnit test.
* **E2E with Visual Proof**: `dotnet test tests/Hackmum.Bethuya.E2E` (Chromium/WebKit/Firefox; tracing on failures). [\[playwright.dev\]](https://playwright.dev/dotnet/docs/intro) Playwright MCP must capture screenshots of UI changes before completion.
* Use **Copilot Chat/Edits** to scaffold tests, then harden assertions manually.
* Prefer `data-test` selectors to keep tests stable with Blazor Blueprint UI. [\[playwright.dev\]](https://playwright.dev/dotnet/docs/intro)
* **Ralph Loop**: Iterative refinement of code based on automated feedback.
* **Self-Correction**: Every developer/agent mistake is recorded in tasks/lessons.md to prevent recurrence.

***

## 📊 Benchmarking & Performance

Bethuya is built for speed. We use BenchmarkDotNet and NBomber to ensure our "foundation" is rock solid.

### ⚖️ Performance Targets

* **Registration Hot Path**: p95 < 80ms at 2,500 RPS; p99 < 180ms.
* **Allocation**: Zero-allocation hot paths for ID handling and JSON serialization.
* **Cache Efficiency**: >90% L1/Redis hit rate for event metadata.
* **Memory**: Steady-state usage < 65% of allocated RAM.
* **Resilience**: Circuit breakers on all external AI services.

### Running Benchmarks

```bash
# Run micro-benchmarks
dotnet run -c Release --project tests/Bethuya.Benchmarks

# Run load tests (requires Aspire AppHost running)
pwsh ./scripts/load-test.ps1 --rps 2500
```
  
## 🚀 Getting Started

### Prerequisites

* **.NET 10 SDK**, **.NET MAUI** workload, **.NET Aspire** workload. [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui?view=aspnetcore-10.0), [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/dotnet/aspire/)
* Platform tooling for your targets (Android/iOS/macOS/Windows). [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0)

### Clone & Restore

```bash
git clone https://github.com/HackerspaceMumbai/Bethuya.git
cd Bethuya
dotnet restore
```

### Configure AI Providers (privacy‑aware)

```bash
# Configure once at Aspire AppHost; secrets propagate to services
cd aspire/Hackmum.Bethuya.AppHost
dotnet user-secrets init

# Sensitive flows local-first:
dotnet user-secrets set "Ai:Provider" "FoundryLocal"
dotnet user-secrets set "Ai:Fallback"  "Ollama"

# Cloud (non-sensitive/public drafts):
dotnet user-secrets set "AzureOpenAI:Endpoint" "<endpoint>"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<key>"
dotnet user-secrets set "OpenAI:ApiKey" "<key>"
```

*(Foundry Local offers an OpenAI‑compatible API and SDKs for integration.)* [\[github.com\]](https://github.com/microsoft/Foundry-Local)

```bash
dotnet run --project aspire/Hackmum.Bethuya.AppHost
# The Aspire Dashboard opens with links to services & health.
```

[\[aspire.dev\]](https://aspire.dev/dashboard/explore/)

### Build the UI (Blazor Blueprint UI)

No build step required. Just add the NuGet package and reference the stylesheet as above.

### Run on platforms (direct)

```bash
# Windows (WinUI)
dotnet run --project src/Hackmum.Bethuya.App -f net10.0-windows10.0.19041.0

# Android
dotnet build src/Hackmum.Bethuya.App -t:Run -f net10.0-android

# iOS (on macOS)
dotnet build src/Hackmum.Bethuya.App -f net10.0-ios

# macOS (MacCatalyst)
dotnet run --project src/Hackmum.Bethuya.App -f net10.0-maccatalyst
```

[\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0)

***

## ⚙️ `Directory.Build.props` (starter)

```xml
<Project>
  <PropertyGroup>
    <TargetFrameworks>
      net10.0;net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows10.0.19041.0
    </TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>preview</LangVersion>
    <Deterministic>true</Deterministic>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Update="TUnit" Version="*" />
    <PackageReference Update="Microsoft.Playwright.NUnit" Version="*" />
    <!-- Microsoft Agent Framework / SK once pinned -->
    <!-- <PackageReference Update="Microsoft.AgentFramework" Version="*" /> -->
    <!-- <PackageReference Update="Microsoft.SemanticKernel" Version="*" /> -->
  </ItemGroup>
</Project>
```

***

## 🔐 Privacy, Safety, and DEI

* **Consent first** — only use self‑provided, consented fields for curation.
* **No inference** of sensitive traits; no opaque scoring.
* **Explainable** suggestions; human approvals required.
* **Auditability** — logs & traces in Aspire Dashboard. [\[aspire.dev\]](https://aspire.dev/dashboard/explore/)
* **Local‑first** for sensitive flows via **Foundry Local** (offline, on‑device). [\[devblogs.m...rosoft.com\]](https://devblogs.microsoft.com/foundry/unlock-instant-on-device-ai-with-foundry-local/), [\[github.com\]](https://github.com/microsoft/Foundry-Local)

***

## 🧭 Demo Flow (Dev Days–ready)

1. **Create Event** — “GitHub Copilot Dev Days: Mumbai”.
2. **Planner** — review agenda proposals → approve subset.
3. **Curator (Attendees)** — set capacity → review **FairnessBudget** + **AttendanceProposal** → accept → **WaitlistProposal** generated.
4. **Live** — facilitator prompts (opt‑in) and note capture.
5. **Report** — reporter draft → minimal edits → publish with attribution.
6. **Dev AI** — run `/run-e2e` skill (Playwright), inspect traces; use **Copilot CLI** to `/plan` a refactor and open a PR. [\[playwright.dev\]](https://playwright.dev/dotnet/docs/intro), [\[github.blog\]](https://github.blog/changelog/2026-02-25-github-copilot-cli-is-now-generally-available/)

***

## 👥 Contributing (Copilot‑first, Human‑reviewed)

* Namespace: **`Hackmum.Bethuya.*`**
* Commit: `type(scope): message` (e.g., `feat(curator): fairness budget model`)
* Use **Copilot Chat/Edits** + **Copilot SDK skills** for boilerplate, tests, refactors, docs — **humans** review agent logic/prompts, DEI safeguards, and data handling. [\[github.com\]](https://github.com/github/copilot-sdk/blob/main/docs/guides/skills.md)
* Recommended VS Code Insiders extensions: Copilot (Chat/Edits), C# Dev Kit, .NET Aspire, Dev Containers, GitHub PRs. [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/dotnet/aspire/)

***

## 🛣 Roadmap

### Hackathon Scope

* [ ] Event model & storage
* [ ] Planner + **Curator (attendees)** agents
* [ ] Diff + approval workflow (HIL)
* [ ] Reporter draft
* [ ] Aspire AppHost + telemetry

### Post‑Hackathon (Backbone Goals)

* [ ] Speaker proposal portal & review
* [ ] Multi‑event dashboards, templates, recurrence
* [ ] Public event pages (export to Markdown/GitHub Pages)
* [ ] RSVP + QR check‑in
* [ ] Transcription & highlights
* [ ] Plugin‑style agent extensibility (via **Microsoft Agent Framework**) [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/agent-framework/)
* [ ] Offline‑first sync model
* [ ] Multi‑chapter hierarchy
* [ ] Advanced analytics & insights

***

## 📜 License

**MIT** — for maximum community adoption.

***

## 🙌 Acknowledgments

Built by and for the **HackerspaceMumbai** community.\
Powered by **.NET 10**, **.NET Aspire**, **Blazor Hybrid (MAUI)**, **Blazor Blueprint UI**, **Microsoft Agent Framework**, **Foundry Local**, and **GitHub Copilot + VS Code Insiders**. [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/dotnet/aspire/), [\[blazorblueprintui.com\]](https://blazorblueprintui.com/), [\[learn.microsoft.com\]](https://learn.microsoft.com/en-us/agent-framework/), [\[devblogs.m...rosoft.com\]](https://devblogs.microsoft.com/foundry/unlock-instant-on-device-ai-with-foundry-local/), [\[github.blog\]](https://github.blog/changelog/2026-02-25-github-copilot-cli-is-now-generally-available/)

***
