using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Repositories;

public interface IDecisionRepository
{
    Task<Decision?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Decision>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<IReadOnlyList<Decision>> GetPendingAsync(CancellationToken ct = default);
    Task<Decision> CreateAsync(Decision entity, CancellationToken ct = default);
    Task UpdateAsync(Decision entity, CancellationToken ct = default);
}
