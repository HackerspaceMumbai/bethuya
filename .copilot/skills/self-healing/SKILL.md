# Skill: Self-Healing & Diagnostic Loop

## Trigger: TUnit Failure

When a test run (e.g., `dotnet test` using TUnit) returns a non-zero exit code:
1. **Identify the Culprit:** Parse the TUnit output to identify which service or integration point failed.
   - If parsing fails or multiple failures are detected, log all failures and process them sequentially.
   - If the failing resource cannot be determined, escalate to manual review.
3. **Correlate with Traces:** If the logs show a `HttpRequestException`, timeout, database connection failure, authentication error, or resource exhaustion, use the Aspire MCP to query the OpenTelemetry traces for that specific `TraceId`.
   - `aspire logs <resource-name> --tail 100`
## Fix & Verify Protocol

0. **Log Healing Attempt:** Record the start of a self-healing attempt with timestamp, resource name, and failure details.
1. **Analyze:** Neo must compare the log error (e.g., "Connection refused") with the `AppHost` configuration.
2. **Apply Fix:** Cypher or Trinity applies the code fix.
   - `aspire resource restart <resource-name>`
   - If restart fails, log the error, increment the failure counter, and proceed to step 5.
   - `aspire resource restart <resource-name>`
4. **Final Check:** Wait for the resource to be healthy (`aspire wait <resource-name>`) before re-running the TUnit suite.
5. **Failure Limit:** Stop the self-healing loop after 2 consecutive failures to prevent infinite loops and resource thrashing.
   - Log all healing attempts and outcomes.
   - Alert the on-call team if the failure limit is reached.
   - Consider implementing a rollback mechanism if the fix degrades the service further.
4. **Final Check:** Wait for the resource to be healthy (`aspire wait <resource-name>`) before re-running the TUnit suite.
5. **Failure Limit:** Stop the self-healing loop after 2 consecutive failures to prevent infinite loops and resource thrashing.