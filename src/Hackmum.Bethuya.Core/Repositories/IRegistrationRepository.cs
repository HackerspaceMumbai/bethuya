using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Repositories;

public interface IRegistrationRepository
{
    Task<Registration?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Registration>> GetByEventIdAsync(Guid eventId, CancellationToken ct = default);
    Task<Registration> CreateAsync(Registration entity, CancellationToken ct = default);
    Task UpdateAsync(Registration entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
