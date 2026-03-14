# Skill: scaffold-agent

## Description
Scaffold a new Microsoft Agent Framework (MAF) domain agent with the correct Bethuya structure — including the `IAgent` implementation, tool registrations, memory configuration, AppHost wiring, and TUnit test stubs.

## Trigger
Use when adding a new domain agent to the platform. Invoke with `/scaffold-agent` in Copilot CLI.

## Prerequisites
- `src/Bethuya.Agents/` project exists
- Microsoft Agent Framework package in `Directory.Packages.props`
- Persona file exists in `.github/agents/<agent-name>.md`

## Steps
1. Read the persona file at `.github/agents/<agent-name>.md` to understand inputs, outputs, guardrails, and tools.
2. Scaffold the MAF agent class in `src/Bethuya.Agents/<AgentName>Agent.cs`:
   - Implement `IAgent` (or appropriate MAF base class)
   - Declare tools as `[AgentTool]` methods matching the persona's "Tools Available" section
   - Configure memory: session-scoped (Facilitator) or persistent keyed by event ID (Planner, Curator, Reporter)
   - Add `[RequiresLocalProvider]` on any tool handling PII (Curator only)
3. Scaffold the worker class in `src/Bethuya.Agents/<AgentName>Worker.cs` for background processing.
4. Register the agent and worker in `AppHost/AppHost/AppHost.cs`:
   ```csharp
   builder.AddProject<Projects.Bethuya_Agents>("<agent-name>-worker");
   ```
5. Scaffold output model(s) in `src/Bethuya.Core/Models/` based on the persona's Outputs section.
6. Create TUnit test stubs in `tests/Bethuya.TUnit.Tests/Agents/<AgentName>AgentTests.cs`:
   - One test per guardrail in the persona
   - One happy-path test for the primary output
   - One test verifying human-in-the-loop contract (output is always a draft/proposal, never auto-published)

## AI Provider Assignment (by agent)
| Agent | Provider | Reason |
|---|---|---|
| Planner | Azure OpenAI | Non-sensitive event planning |
| Curator | Foundry Local | PII / sensitive attendee data |
| Facilitator | Foundry Local / Ollama | Real-time, offline-capable |
| Reporter | Azure OpenAI | Non-sensitive public summaries |
| New agents | Ask: does it touch PII? → Foundry Local, else Azure OpenAI |

## Expected Output
- `src/Bethuya.Agents/<AgentName>Agent.cs` — MAF agent with TODO markers
- `src/Bethuya.Agents/<AgentName>Worker.cs` — background worker stub
- `src/Bethuya.Core/Models/<OutputModel>.cs` — output model(s)
- `tests/Bethuya.TUnit.Tests/Agents/<AgentName>AgentTests.cs` — TUnit stubs (red phase)
- `AppHost/AppHost/AppHost.cs` — updated with worker registration

## Notes
- New agents must have a persona file in `.github/agents/` before scaffolding.
- All agents must follow the human-in-the-loop contract: outputs are drafts/proposals, never auto-published.
- Guardrails must be tested — do not scaffold without corresponding TUnit stubs.
