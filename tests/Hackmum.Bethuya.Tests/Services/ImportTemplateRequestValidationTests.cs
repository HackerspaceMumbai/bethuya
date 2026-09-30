using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class ImportTemplateRequestValidationTests
{
    private static readonly ImportColumnMappingResponse EmailMapping = new("Email", ImportTargetField.Email);

    [Test]
    public async Task ValidRequest_ReturnsNull()
    {
        var errors = ImportEndpoints.ValidateTemplateRequest("Luma copy", [EmailMapping]);

        await Assert.That(errors).IsNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task BlankName_IsRejected(string? name)
    {
        var errors = ImportEndpoints.ValidateTemplateRequest(name, [EmailMapping]);

        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!.ContainsKey("name")).IsTrue();
    }

    [Test]
    public async Task OverlongName_IsRejected()
    {
        var errors = ImportEndpoints.ValidateTemplateRequest(
            new string('n', ImportEndpoints.MaxTemplateTextLength + 1), [EmailMapping]);

        await Assert.That(errors!.ContainsKey("name")).IsTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments("  ")]
    public async Task BlankSourceColumn_IsRejected(string source)
    {
        var errors = ImportEndpoints.ValidateTemplateRequest("Template", [new(source, ImportTargetField.Email)]);

        await Assert.That(errors!.ContainsKey("columnMappings")).IsTrue();
    }

    [Test]
    public async Task OverlongSourceColumn_IsRejected()
    {
        var errors = ImportEndpoints.ValidateTemplateRequest(
            "Template",
            [new(new string('c', ImportEndpoints.MaxTemplateTextLength + 1), ImportTargetField.Email)]);

        await Assert.That(errors!.ContainsKey("columnMappings")).IsTrue();
    }
}
