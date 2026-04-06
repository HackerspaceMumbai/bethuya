# Skill: Self-Healing & Diagnostic Loop

## Trigger: TUnit Failure

When a test run (e.g., `dotnet test` using TUnit) returns a non-zero exit code:
1. **Identify the Culprit:** Parse the TUnit output to identify which service or integration point failed.
2. **Retrieve Logs:** Use the Aspire CLI to fetch the last 100 lines of logs for the specific resource:
   - `aspire logs <resource-name> --tail 100`
3. **Correlate with Traces:** If the logs show a `HttpRequestException` or timeout, use the Aspire MCP to query the OpenTelemetry traces for that specific `TraceId`.

## Fix & Verify Protocol

1. **Analyze:** Neo must compare the log error (e.g., "Connection refused") with the `AppHost` configuration.
2. **Apply Fix:** Cypher or Trinity applies the code fix.
3. **Restart Resource:** Neo triggers a targeted restart of the service:
   - `aspire resource restart <resource-name>`
4. **Final Check:** Wait for the resource to be healthy (`aspire wait <resource-name>`) before re-running the TUnit suite.
5. **Failure Limit:** Stop the self-healing loop after 2 consecutive failures to prevent infinite loops and resource thrashing.