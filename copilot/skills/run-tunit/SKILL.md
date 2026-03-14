# Skill: run-tunit

## Description
Run the TUnit test suite for Bethuya. Supports single-run, filtered, and watch-mode TDD loops. Summarizes failures with assertion details for fast iteration.

## Trigger
Use during TDD red-green-refactor cycles, before committing, or to verify a specific domain area. Invoke with `/run-tunit` in Copilot CLI.

## Trigger Scenarios
- Starting a new feature → watch mode TDD loop
- Verifying a bug fix → filtered single-run
- Pre-commit check → full single-run
- Debugging a specific failure → filtered run with verbose output

## Steps

### Single run (default)
```bash
dotnet test tests/Bethuya.TUnit.Tests/ --logger trx --results-directory TestResults/
```

### Filtered run
```bash
dotnet test tests/Bethuya.TUnit.Tests/ --filter "<filter-expression>"
# Examples:
#   --filter "FullyQualifiedName~CuratorAgent"   ← all Curator tests
#   --filter "Category=Unit"                      ← unit tests only
#   --filter "Category=Integration"               ← integration tests only
```

### Watch mode (TDD loop)
```bash
dotnet watch test --project tests/Bethuya.TUnit.Tests/
```
Watch mode re-runs tests automatically on file save. Use this during active TDD cycles.

## Parameters
- `--filter` — TUnit/dotnet test filter expression
- `--watch` — enable watch mode (default: off)
- `--verbose` — show full assertion output for failures (default: failures only)
- `--area` — shortcut filters: `agents`, `domain`, `infra`, `ui` (maps to `FullyQualifiedName~`)

## Expected Output
```
TUnit Results: 47 passed, 2 failed, 3 skipped.

FAILED: CuratorAgentTests.WhenDeiFieldsNotConsented_DoesNotUseForSelection
  Assert.That(result.UsedFields).DoesNotContain("ethnicity")
  Actual: ["theme_interest", "ethnicity"]   ← DEI field used without consent

FAILED: EventPlannerTests.WhenVenueCapacityExceeded_SurfacesTrade-off
  Assert.That(draft.Warnings).IsNotEmpty()
  Actual: []   ← no warnings emitted
```

## TDD Protocol
Per the Bethuya development protocol:
1. Write a failing TUnit test first (red)
2. Run `/run-tunit --filter <test-name>` to confirm it fails
3. Write the minimum code to make it pass (green)
4. Run again to confirm pass
5. Refactor — run again to confirm still green
6. Commit when all tests pass

## Notes
- TUnit is the **only** test framework in this solution — do not introduce xUnit or NUnit.
- Test class naming: `<ClassUnderTest>Tests` (e.g., `CuratorAgentTests`)
- Test method naming: `<When/Given>_<Condition>_<ExpectedOutcome>` (e.g., `WhenCapacityExceeded_ProducesAttendanceProposal`)
- Use `[Category("Unit")]` or `[Category("Integration")]` attributes to enable filtered runs.
- Integration tests that need the database should use an in-memory provider or test containers.
