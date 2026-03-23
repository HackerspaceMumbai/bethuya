using System.ComponentModel.DataAnnotations;
using Bethuya.Hybrid.Shared.Models;

namespace Hackmum.Bethuya.Tests.Domain;

public class CreateEventFormModelTests
{
    [Test]
    public async Task ValidModel_PassesValidation()
    {
        var model = CreateValidModel();
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task EmptyTitle_FailsValidation()
    {
        var model = CreateValidModel();
        model.Title = "";
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(CreateEventFormModel.Title)))).IsTrue();
    }

    [Test]
    public async Task TitleOver200Chars_FailsValidation()
    {
        var model = CreateValidModel();
        model.Title = new string('A', 201);
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(CreateEventFormModel.Title)))).IsTrue();
    }

    [Test]
    public async Task TitleExactly200Chars_PassesValidation()
    {
        var model = CreateValidModel();
        model.Title = new string('A', 200);
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task CapacityZero_FailsValidation()
    {
        var model = CreateValidModel();
        model.Capacity = 0;
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(CreateEventFormModel.Capacity)))).IsTrue();
    }

    [Test]
    public async Task CapacityAboveMax_FailsValidation()
    {
        var model = CreateValidModel();
        model.Capacity = 10_001;
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(CreateEventFormModel.Capacity)))).IsTrue();
    }

    [Test]
    public async Task CapacityAtBounds_PassesValidation()
    {
        var model = CreateValidModel();
        model.Capacity = 1;
        await Assert.That(ValidateModel(model)).IsEmpty();

        model.Capacity = 10_000;
        await Assert.That(ValidateModel(model)).IsEmpty();
    }

    [Test]
    public async Task EndDateBeforeStartDate_FailsValidation()
    {
        var model = CreateValidModel();
        model.StartDate = new DateOnly(2026, 6, 15);
        model.EndDate = new DateOnly(2026, 6, 14);
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(CreateEventFormModel.EndDate)))).IsTrue();
    }

    [Test]
    public async Task EndDateEqualsStartDate_PassesValidation()
    {
        var model = CreateValidModel();
        model.StartDate = new DateOnly(2026, 6, 15);
        model.EndDate = new DateOnly(2026, 6, 15);
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task DescriptionOver2000Chars_FailsValidation()
    {
        var model = CreateValidModel();
        model.Description = new string('B', 2001);
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(CreateEventFormModel.Description)))).IsTrue();
    }

    [Test]
    public async Task NullDescription_PassesValidation()
    {
        var model = CreateValidModel();
        model.Description = null;
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task NegativeCapacity_FailsValidation()
    {
        var model = CreateValidModel();
        model.Capacity = -1;
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(CreateEventFormModel.Capacity)))).IsTrue();
    }

    [Test]
    public async Task MaxBoundaryCapacity_PassesValidation()
    {
        var model = CreateValidModel();
        model.Capacity = 10_000;
        var results = ValidateModel(model);

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task OverMaxCapacity_FailsValidation()
    {
        var model = CreateValidModel();
        model.Capacity = 10_001;
        var results = ValidateModel(model);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.Any(r => r.MemberNames.Contains(nameof(CreateEventFormModel.Capacity)))).IsTrue();
    }

    private static CreateEventFormModel CreateValidModel() => new()
    {
        Title = "Community AI Hackathon",
        Description = "A weekend hackathon",
        Type = "Hackathon",
        Capacity = 50,
        Location = "Microsoft Reactor",
        StartDate = new DateOnly(2026, 7, 1),
        EndDate = new DateOnly(2026, 7, 2)
    };

    private static List<ValidationResult> ValidateModel(CreateEventFormModel model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
