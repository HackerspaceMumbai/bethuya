namespace Bethuya.Hybrid.Shared.Components.Dashboard;

public sealed class EventViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Type { get; set; } = "Meetup";
    public string Status { get; set; } = "Draft";
    public string? AgendaStatus { get; set; }
    public int Capacity { get; set; } = 100;
    public string? Location { get; set; }
    public string? Hashtag { get; set; }
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
}
