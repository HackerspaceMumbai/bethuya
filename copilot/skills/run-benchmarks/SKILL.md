# Skill: run-benchmarks

## Description
Run BenchmarkDotNet micro-benchmarks and NBomber load tests for the Bethuya hot paths. Validates performance targets and reports regressions.

## Trigger
Use when implementing a hot-path feature, before merging performance-sensitive changes, or when a performance regression is suspected. Invoke with `/run-benchmarks` in Copilot CLI.

## Performance Targets
| Metric | Target |
|---|---|
| Registration hot path (p95) | < 80ms @ 2,500 RPS |
| Registration hot path (p99) | < 180ms @ 2,500 RPS |
| Hot-path memory allocation | 0 B |
| Cache hit rate | > 90% |
| Steady-state memory | < 65% allocated RAM |

## Prerequisites
- .NET 10 SDK installed
- `tests/Bethuya.Benchmarks` project exists with BenchmarkDotNet configured
- For load tests: Aspire AppHost running + PowerShell 7+

## Steps

### Micro-benchmarks (BenchmarkDotNet)
1. Run: `dotnet run -c Release --project tests/Bethuya.Benchmarks`
2. Parse the results table from stdout.
3. Compare each benchmark against the performance targets above.
4. Flag any benchmark that exceeds its target as a regression.
5. Report: benchmark name, measured value, target, pass/fail.

### Load tests (NBomber)
1. Verify the Aspire AppHost is running (`dotnet run --project AppHost/AppHost`).
2. Run: `pwsh ./scripts/load-test.ps1 --rps 2500`
3. Parse the NBomber HTML report in `TestResults/LoadTest/`.
4. Extract p95, p99 latency and error rate.
5. Flag regressions against targets.

## Parameters
- `--type` — `micro` (BenchmarkDotNet only), `load` (NBomber only), `all` (default)
- `--filter` — BenchmarkDotNet filter (e.g., `--filter *RegistrationHotPath*`)

## Expected Output
```
Micro-benchmarks:
  ✓ RegistrationHotPath     — 0 B alloc    (target: 0 B)
  ✓ EventMetadataCache      — 12ms mean    (target: <80ms p95)
  ✗ AttendeeListQuery       — 220ms p99    (target: <180ms) ← REGRESSION

Load test (2,500 RPS):
  ✓ p95: 74ms   (target: <80ms)
  ✗ p99: 195ms  (target: <180ms) ← REGRESSION
```

## Notes
- Always run with `-c Release` — Debug builds have different allocation patterns.
- Do not run load tests in CI on PRs — only on `main` (see `ci.yml` `e2e` job pattern).
- BenchmarkDotNet artifacts are in `BenchmarkDotNet.Artifacts/`.
