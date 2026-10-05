using Bethuya.Hybrid.Shared.Models.OpportunityEngine;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Provides the Opportunity Engine operational workspace projection.</summary>
public interface IOpportunityEngineService
{
    /// <summary>Gets a display-ready Opportunity Engine workspace.</summary>
    Task<OpportunityEngineWorkspace> GetWorkspaceAsync(CancellationToken cancellationToken = default);
}
