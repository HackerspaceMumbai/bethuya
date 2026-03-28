using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Repositories;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Event?> GetByHashtagAsync(string hashtag, CancellationToken ct = default);
    Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default);
    Task<Event> CreateAsync(Event entity, CancellationToken ct = default);
    Task UpdateAsync(Event entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
