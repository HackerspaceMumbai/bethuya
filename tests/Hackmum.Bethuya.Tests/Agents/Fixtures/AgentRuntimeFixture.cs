namespace Hackmum.Bethuya.Tests.Agents.Fixtures;

/// <summary>
/// Phase 1: Aspire + Agent Runtime integration test fixture stub.
/// Tank/Trinity will implement full Aspire hosting integration in Phase 2.
/// </summary>
public sealed class AgentRuntimeFixture
{
    // Phase 1: Placeholder — full Aspire integration in Phase 2

    /// <summary>
    /// Phase 2: Returns Aspire service endpoint for Orchestrator API.
    /// </summary>
    public string GetOrchestratorEndpoint()
    {
        throw new NotImplementedException("Phase 2: Aspire integration pending");
    }

    /// <summary>
    /// Phase 2: Returns Aspire service endpoint for Approver API.
    /// </summary>
    public string GetApproverEndpoint()
    {
        throw new NotImplementedException("Phase 2: Aspire integration pending");
    }

    /// <summary>
    /// Phase 2: Seeds test database with initial data.
    /// </summary>
    public Task SeedDatabaseAsync()
    {
        throw new NotImplementedException("Phase 2: Database seeding pending");
    }

    /// <summary>
    /// Phase 2: Cleans test database after test.
    /// </summary>
    public Task CleanDatabaseAsync()
    {
        throw new NotImplementedException("Phase 2: Database cleanup pending");
    }
}
