using System.Text.Json.Serialization;
using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

public sealed class Registration
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public string? Bio { get; set; }
    public List<string> Interests { get; set; } = [];
    public string? Intent { get; set; }
    public string? Goals { get; set; }
    public List<string> ContributionPreferences { get; set; } = [];
    public string? ExperienceLevel { get; set; }
    public string? AttendanceLikelihood { get; set; }
    public string? TravelRequirement { get; set; }
    public string? DietaryRequirements { get; set; }
    public string? AccessibilityNeeds { get; set; }
    [JsonIgnore]
    public string? GovernmentIdFileName { get; set; }
    [JsonIgnore]
    public string? GovernmentIdContentType { get; set; }
    [JsonIgnore]
    public string? GovernmentIdProtectedPayload { get; set; }
    [JsonIgnore]
    public DateTimeOffset? GovernmentIdUploadedAt { get; set; }
    public InclusionSignals InclusionSignals { get; set; } = new();
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Event? Event { get; init; }
}
