using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Repositories;

/// <summary>Data access interface for attendee profiles.</summary>
public interface IAttendeeProfileRepository
{
    /// <summary>Gets a profile by the authentication provider subject identifier.</summary>
    Task<AttendeeProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>Persists a new attendee profile.</summary>
    Task<AttendeeProfile> CreateAsync(AttendeeProfile profile, CancellationToken ct = default);

    /// <summary>Updates an existing attendee profile.</summary>
    Task UpdateAsync(AttendeeProfile profile, CancellationToken ct = default);

    /// <summary>Returns a minimal profile projection used for inclusion-signal derivation.</summary>
    Task<AttendeeInclusionSource?> GetInclusionSourceByEmailAsync(string email, CancellationToken ct = default);
}
