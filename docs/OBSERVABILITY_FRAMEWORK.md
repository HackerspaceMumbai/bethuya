# Bethuya Observability Framework

This document describes the implementation contract for Bethuya observability across web apps, APIs, workers, and hosted agents.

## Core design

1. **Reuse shared defaults:** all executable services must call `builder.AddServiceDefaults()`.
2. **Keep telemetry portable:** OTLP is the primary contract; Azure Monitor is an optional reference backend.
3. **Secure by default:** no secrets or PII in telemetry payloads.
4. **Performance-safe signals:** avoid high-cardinality metric dimensions and keep trace metadata bounded.

## Current implementation

### ServiceDefaults

`ServiceDefaults/Extensions.cs` is the source of truth for shared telemetry behavior:

- OpenTelemetry logs, traces, and metrics are enabled for all services.
- Exporters are environment-driven:
  - `OTEL_EXPORTER_OTLP_ENDPOINT` enables OTLP export.
  - `APPLICATIONINSIGHTS_CONNECTION_STRING` enables Azure Monitor export.
- Resource attributes are enriched with:
  - `deployment.environment.name`
  - `service.version`
  - `service.build.id`
- Health endpoints are standardized:
  - `/health`, `/alive`
  - compatibility aliases: `/healthz`, `/livez`

### Planner/agent tracing

Planner invocations use existing trace propagation and persistence patterns:

- `traceparent` and `x-correlation-id` are passed to the hosted planner API.
- Planning-cycle persistence normalizes and bounds trace/provider metadata before storage.
- Planner invocation spans are tagged with:
  - `gen_ai.system`
  - `gen_ai.request.model`
  - `gen_ai.operation.name`
  - `mcp.server.identity`
  - `mcp.tool.name`

## Guardrails

### Security

- Do not emit access tokens, API keys, passwords, emails, or other PII into logs/traces/metrics.
- Keep any request correlation identifiers synthetic and non-user-derived.

### Cardinality and cost

- Do not add high-cardinality identifiers (`UserId`, `Email`, `PromptHash`, `SessionId`) as metric dimensions.
- High-cardinality troubleshooting context belongs in traces/log scopes only, and only when non-sensitive.

### Reliability

- New services and jobs are not production-ready until they expose shared health endpoints.
- Any new agent flow must preserve `traceparent` propagation across service boundaries.

## Validation expectations

- Backend integration tests should continue to verify `/health` and `/alive`.
- Compatibility aliases `/healthz` and `/livez` must return success.
- Planner telemetry tags should be covered by focused unit tests in `Hackmum.Bethuya.Tests`.

## Rollout phases

1. **Foundations:** shared defaults + deployment/resource attributes + planner span tags.
2. **Dashboard parity:** align Azure Workbooks and OSS Grafana dashboards from common OTEL signals.
3. **Operational maturity:** saved runbook queries, alert ownership mapping, and service-map playbooks.
