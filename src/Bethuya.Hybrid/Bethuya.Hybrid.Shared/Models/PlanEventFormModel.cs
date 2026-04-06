using System.ComponentModel.DataAnnotations;

namespace Bethuya.Hybrid.Shared.Models;

/// <summary>
/// Form model for planning a new event. Validation strictness depends on <see cref="Status"/>:
/// <list type="bullet">
///   <item><b>Draft</b> — only Title is required (quick save).</item>
///   <item><b>Any other status</b> — all fields validated (publish-ready).</item>
/// </list>
/// </summary>
public sealed class PlanEventFormModel : IValidatableObject
{
    /// <summary>Event lifecycle status. Defaults to Draft for minimal-validation quick saves.</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>Whether the status represents a Draft (minimal validation).</summary>
    public bool IsDraft => string.Equals(Status, "Draft", StringComparison.OrdinalIgnoreCase);

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
    public string Title { get; set; } = "";

    [MaxLength(2000, ErrorMessage = "Description must be 2,000 characters or fewer.")]
    public string? Description { get; set; }

    public string Type { get; set; } = "Meetup";

    [Range(1, 10_000, ErrorMessage = "Capacity must be between 1 and 10,000.")]
    public int Capacity { get; set; } = 100;

    [MaxLength(300, ErrorMessage = "Location must be 300 characters or fewer.")]
    public string? Location { get; set; }

    [MaxLength(100, ErrorMessage = "Hashtag must be 100 characters or fewer.")]
    [RegularExpression(@"^[A-Za-z][A-Za-z0-9_]*$",
        ErrorMessage = "Hashtag must start with a letter and contain only letters, digits, and underscores.")]
    public string? Hashtag { get; set; }

    /// <summary>Public URL of the uploaded cover image (set after successful upload).</summary>
    [MaxLength(2048, ErrorMessage = "Cover image URL must be 2,048 characters or fewer.")]
    public string? CoverImageUrl { get; set; }

    public DateTime? StartDate { get; set; } = DateTime.Today;

    public TimeSpan? StartTime { get; set; } = new TimeSpan(9, 0, 0);

    public DateTime? EndDate { get; set; } = DateTime.Today;

    public TimeSpan? EndTime { get; set; } = new TimeSpan(17, 0, 0);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Draft mode: Title (via [Required]) is the only hard requirement.
        if (IsDraft)
            yield break;

        // Publish mode: enforce all fields.
        if (string.IsNullOrWhiteSpace(Type))
            yield return new ValidationResult("Event type is required to publish.", [nameof(Type)]);

        if (Capacity < 1)
            yield return new ValidationResult("Capacity is required to publish.", [nameof(Capacity)]);

        if (StartDate is null)
            yield return new ValidationResult("Start date is required to publish.", [nameof(StartDate)]);

        if (EndDate is null)
            yield return new ValidationResult("End date is required to publish.", [nameof(EndDate)]);

        if (StartDate is not null && EndDate is not null)
        {
            var startDt = StartDate.Value.Date + (StartTime ?? TimeSpan.Zero);
            var endDt = EndDate.Value.Date + (EndTime ?? TimeSpan.Zero);

            if (endDt < startDt)
            {
                yield return new ValidationResult(
                    "End date and time must be on or after the start date and time.",
                    [nameof(EndDate)]);
            }
        }
    }
}
