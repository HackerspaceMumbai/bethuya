# Skill: run-e2e

## Description
Run the Playwright for .NET E2E test suite and summarize results, including failure traces and screenshots.

> This repository's E2E suite lives in `tests/Hackmum.Bethuya.E2E/` (Playwright for .NET).

## Trigger
Use before merging any UI changes. Invoke with `/run-e2e` in Copilot CLI.

## Prerequisites
1. Aspire AppHost running (`dotnet run --project AppHost/AppHost`)
2. Set base URL for the running app instance:
   - PowerShell: `$env:BETHUYA_BASE_URL='https://localhost:7400'`
3. Playwright browser runtime available for .NET tests.

> 💡 **Playwright MCP alternative:** If you're working interactively in Copilot CLI, the Playwright MCP server can drive browser actions directly without running a separate test suite. Use `/mcp` to check if it's configured.

## Steps
1. Verify the Aspire AppHost is running and all resources are healthy.
2. Run focused seam check first (faster regression signal), then broader suite if needed.
3. Run:
   - `dotnet test tests/Hackmum.Bethuya.E2E/Hackmum.Bethuya.E2E.csproj --verbosity minimal`
4. Parse TRX results for failures.
4. For each failure:
   - Locate the Playwright trace file in `TestResults/` (`*.zip` trace archive)
   - Extract the failing step, screenshot, and assertion message
   - Summarize: test name → failing step → expected vs actual
5. Report: total passed, failed, skipped. List failures with trace file paths.
6. If UI shows generic failure text, collect Aspire `backend`/`web` console logs and include likely root cause in summary.

## Parameters
- `--filter` — test name filter (e.g., `--filter "PlanEvent_WithoutCoverImage_ShouldSucceed"`)
- `--browser` — browser to run (chromium | webkit | firefox; default: chromium)
- `--headed` — run in headed mode for debugging (not for CI)

## Expected Output
```
E2E Results: 24 passed, 1 failed, 0 skipped.

FAILED: AttendeeRegistration_SubmitForm_ShowsConfirmation
  Step: Click [data-test="submit-btn"]
  Error: Timeout waiting for [data-test="confirmation-message"]
  Trace: TestResults/traces/AttendeeRegistration_trace.zip
```

## Notes
- Always use `data-test` selectors in Razor components — never CSS classes or element types.
- Traces are auto-captured on failure when `Tracing.StartAsync` is configured in test setup.
- Run with `--browser webkit` to catch Safari/iOS-specific issues before mobile releases.
- Trace archives can be opened with `dotnet tool run playwright show-trace <file.zip>`.
- Keep one seam-level test for every previously broken integration boundary (script load, publish transaction, upload flow).
