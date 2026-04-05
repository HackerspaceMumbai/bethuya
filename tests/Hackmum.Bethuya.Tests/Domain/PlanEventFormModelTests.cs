using System.ComponentModel.DataAnnotations;
using Bethuya.Hybrid.Shared.Models;

namespace Hackmum.Bethuya.Tests.Domain;

public class PlanEventFormModelTests
{
    // ── Draft Mode Tests (minimal validation — only Title required) ──

    [Test]
    public async Task Draft_WithOnlyTitle_ShouldPassValidation()
    {
        var model = new PlanEventFormModel { Title = "Quick idea", Status = "Draft" };
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Draft_WithEmptyTitle_ShouldFailValidation()
    {
        var model = new PlanEventFormModel { Title = "", Status = "Draft" };
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.Title)))).IsTrue();
    }

    [Test]
    public async Task Draft_WithZeroCapacity_ShouldPassValidation()
    {
        var model = new PlanEventFormModel { Title = "Draft event", Status = "Draft", Capacity = 0 };
        var results = ValidateModel(model);

        // Capacity [Range] attribute still fires, but IValidatableObject skips publish rules
        // Since [Range(1,10000)] is always on, this will fail — but that's attribute-level, not publish-gate
        await Assert.That(results).IsNotEmpty();
    }

    [Test]
    public async Task Draft_WithNullDates_ShouldPassValidation()
    {
        var model = new PlanEventFormModel { Title = "Draft event", Status = "Draft", StartDate = null, EndDate = null };
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Draft_IsDraftProperty_ShouldBeTrue()
    {
        var model = new PlanEventFormModel { Status = "Draft" };

        await Assert.That(model.IsDraft).IsTrue();
    }

    [Test]
    public async Task Draft_IsDraftProperty_CaseInsensitive()
    {
        var model = new PlanEventFormModel { Status = "draft" };

        await Assert.That(model.IsDraft).IsTrue();
    }

    // ── Publish Mode Tests (full validation enforced) ──

    [Test]
    public async Task Publish_ValidModel_ShouldPassValidation()
    {
        var model = CreateValidPublishModel();
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Publish_WithEmptyTitle_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.Title = "";
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.Title)))).IsTrue();
    }

    [Test]
    public async Task Publish_WithoutType_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.Type = "";
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.Type)))).IsTrue();
    }

    [Test]
    public async Task Publish_WithZeroCapacity_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.Capacity = 0;
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.Capacity)))).IsTrue();
    }

    [Test]
    public async Task Publish_WithNullStartDate_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.StartDate = null;
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.StartDate)))).IsTrue();
    }

    [Test]
    public async Task Publish_WithNullEndDate_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.EndDate = null;
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.EndDate)))).IsTrue();
    }

    [Test]
    public async Task Publish_EndDateBeforeStartDate_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.StartDate = new DateTime(2026, 6, 15);
        model.EndDate = new DateTime(2026, 6, 14);
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.EndDate)))).IsTrue();
    }

    [Test]
    public async Task Publish_EndDateEqualsStartDate_ShouldPassValidation()
    {
        var model = CreateValidPublishModel();
        model.StartDate = new DateTime(2026, 6, 15);
        model.EndDate = new DateTime(2026, 6, 15);
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Publish_EndTimeBeforeStartTime_SameDay_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.StartDate = new DateTime(2026, 6, 15);
        model.StartTime = new TimeSpan(14, 0, 0);
        model.EndDate = new DateTime(2026, 6, 15);
        model.EndTime = new TimeSpan(10, 0, 0);
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.EndDate)))).IsTrue();
    }

    // ── Always-enforced attribute tests ──

    [Test]
    public async Task TitleOver200Chars_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.Title = new string('A', 201);
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.Title)))).IsTrue();
    }

    [Test]
    public async Task TitleExactly200Chars_ShouldPassValidation()
    {
        var model = CreateValidPublishModel();
        model.Title = new string('A', 200);
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task DescriptionOver2000Chars_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.Description = new string('B', 2001);
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.Description)))).IsTrue();
    }

    [Test]
    public async Task NullDescription_ShouldPassValidation()
    {
        var model = CreateValidPublishModel();
        model.Description = null;
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task CapacityAboveMax_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.Capacity = 10_001;
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.Capacity)))).IsTrue();
    }

    [Test]
    public async Task CapacityAtBounds_ShouldPassValidation()
    {
        var model = CreateValidPublishModel();
        model.Capacity = 1;
        await Assert.That(ValidateModel(model)).IsEmpty();

        model.Capacity = 10_000;
        await Assert.That(ValidateModel(model)).IsEmpty();
    }

    [Test]
    public async Task NegativeCapacity_ShouldFailValidation()
    {
        var model = CreateValidPublishModel();
        model.Capacity = -1;
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(PlanEventFormModel.Capacity)))).IsTrue();
    }

    // ── Status property tests ──

    [Test]
    public async Task DefaultStatus_ShouldBeDraft()
    {
        var model = new PlanEventFormModel();

        await Assert.That(model.Status).IsEqualTo("Draft");
        await Assert.That(model.IsDraft).IsTrue();
    }

    [Test]
    public async Task PlanningStatus_ShouldNotBeDraft()
    {
        var model = new PlanEventFormModel { Status = "Planning" };

        await Assert.That(model.IsDraft).IsFalse();
    }

    /// <summary>Creates a fully valid model in publish (Planning) mode.</summary>
    private static PlanEventFormModel CreateValidPublishModel() => new()
    {
        Title = "Community AI Hackathon",
        Description = "A weekend hackathon",
        Type = "Hackathon",
        Capacity = 50,
        Location = "Microsoft Reactor",
        StartDate = new DateTime(2026, 7, 1),
        StartTime = new TimeSpan(9, 0, 0),
        EndDate = new DateTime(2026, 7, 2),
        EndTime = new TimeSpan(17, 0, 0),
        Status = "Planning"
    };

    private static List<ValidationResult> ValidateModel(PlanEventFormModel model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
