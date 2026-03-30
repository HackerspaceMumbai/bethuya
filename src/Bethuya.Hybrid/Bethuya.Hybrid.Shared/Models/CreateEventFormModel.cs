using System.ComponentModel.DataAnnotations;

namespace Bethuya.Hybrid.Shared.Models;

/// <summary>Form model for creating a new event draft, with DataAnnotations validation.</summary>
public sealed class CreateEventFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
    public string Title { get; set; } = "";

    [MaxLength(2000, ErrorMessage = "Description must be 2,000 characters or fewer.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Event type is required.")]
    public string Type { get; set; } = "Meetup";

    [Required(ErrorMessage = "Capacity is required.")]
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

    [Required(ErrorMessage = "Start date is required.")]
    public DateTime? StartDate { get; set; } = DateTime.Today;

    public TimeSpan? StartTime { get; set; } = new TimeSpan(9, 0, 0);

    [Required(ErrorMessage = "End date is required.")]
    public DateTime? EndDate { get; set; } = DateTime.Today;

    public TimeSpan? EndTime { get; set; } = new TimeSpan(17, 0, 0);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is null || EndDate is null)
            yield break;

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
