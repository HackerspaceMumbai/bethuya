namespace Bethuya.Hybrid.Shared.Components.Dashboard;

public sealed class EventViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Type { get; set; } = "Meetup";
    public string Status { get; set; } = "Draft";
    public string LifecycleState { get; set; } = "Drafted";
    public string? AgendaStatus { get; set; }
    public int Capacity { get; set; } = 100;
    public string? Location { get; set; }
    public string? Hashtag { get; set; }
    public string? CoverImageUrl { get; set; }
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Registration lifecycle counts; null when the viewer cannot see them (non-organizers).</summary>
    public RegistrationCounts? Registrations { get; set; }
}

/// <summary>Registered → approved → checked-in counts shown on an organizer's event card.</summary>
public sealed record RegistrationCounts(int Registered, int Pending, int Approved, int CheckedIn);
