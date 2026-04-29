using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Agents.Builders;

/// <summary>
/// Fluent builder for test event data.
/// </summary>
public sealed class EventDataBuilder
{
    private Guid _eventId = Guid.CreateVersion7();
    private string _title = "Test Event";
    private string _theme = "GenAI";
    private EventStatus _status = EventStatus.Draft;
    private int _capacity = 50;
    private string _createdBy = "test@example.com";
    private DateTimeOffset _startDate = DateTimeOffset.UtcNow.AddDays(30);
    private DateTimeOffset _endDate = DateTimeOffset.UtcNow.AddDays(30).AddHours(4);

    public static EventDataBuilder CreateEvent() => new();

    public static Guid CreateEventId() => Guid.CreateVersion7();

    public EventDataBuilder WithId(Guid eventId)
    {
        _eventId = eventId;
        return this;
    }

    public EventDataBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public EventDataBuilder WithTheme(string theme)
    {
        _theme = theme;
        return this;
    }

    public EventDataBuilder WithStatus(EventStatus status)
    {
        _status = status;
        return this;
    }

    public EventDataBuilder WithCapacity(int capacity)
    {
        _capacity = capacity;
        return this;
    }

    public EventDataBuilder WithCreatedBy(string createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public EventDataBuilder WithStartDate(DateTimeOffset startDate)
    {
        _startDate = startDate;
        return this;
    }

    public EventDataBuilder WithEndDate(DateTimeOffset endDate)
    {
        _endDate = endDate;
        return this;
    }

    public Event Build()
    {
        return new Event
        {
            // Id is init-only but has a default, so we can't set it after construction
            // Use the default Guid.CreateVersion7() behavior
            Title = _title,
            Description = $"Test event about {_theme}",
            Status = _status,
            Capacity = _capacity,
            StartDate = _startDate,
            EndDate = _endDate,
            CreatedBy = _createdBy
        };
    }
}
