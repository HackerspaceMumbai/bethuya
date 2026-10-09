using Bethuya.Hybrid.Shared.Services;

namespace Bethuya.Hybrid.Shared.Components.Passport;

/// <summary>
/// Identifies whether the portfolio editor should create or update an entry.
/// </summary>
public sealed record PortfolioEditRequest(Guid? Id, UpsertPortfolioEntryDto Entry);
