using Hackmum.Bethuya.AI.CopilotSdk;

namespace Hackmum.Bethuya.Tests.Agents;

/// <summary>
/// Tests for <see cref="DateRecommendationService.ParseResponse"/> —
/// validates JSON parsing of AI-generated date recommendations.
/// Does not require a running CopilotClient (tests the pure parsing logic).
/// </summary>
public class DateRecommendationParseTests
{
    [Test]
    public async Task ParseResponse_ValidJson_ReturnsCorrectDates()
    {
        var json = """
            {
                "startDate": "2026-08-15",
                "startTime": "14:00",
                "endDate": "2026-08-15",
                "endTime": "18:00",
                "reasoning": "Saturday afternoon, avoids monsoon peak"
            }
            """;

        var result = DateRecommendationService.ParseResponse(json);

        await Assert.That(result.StartDate).IsEqualTo(new DateOnly(2026, 8, 15));
        await Assert.That(result.StartTime).IsEqualTo(new TimeOnly(14, 0));
        await Assert.That(result.EndDate).IsEqualTo(new DateOnly(2026, 8, 15));
        await Assert.That(result.EndTime).IsEqualTo(new TimeOnly(18, 0));
        await Assert.That(result.Reasoning).IsEqualTo("Saturday afternoon, avoids monsoon peak");
    }

    [Test]
    public async Task ParseResponse_FullDayEvent_ReturnsCorrectTimes()
    {
        var json = """
            {
                "startDate": "2026-09-19",
                "startTime": "10:00",
                "endDate": "2026-09-19",
                "endTime": "17:00",
                "reasoning": "Full day conference slot"
            }
            """;

        var result = DateRecommendationService.ParseResponse(json);

        await Assert.That(result.StartTime).IsEqualTo(new TimeOnly(10, 0));
        await Assert.That(result.EndTime).IsEqualTo(new TimeOnly(17, 0));
    }

    [Test]
    public async Task ParseResponse_MultiDayEvent_ParsesDifferentDates()
    {
        var json = """
            {
                "startDate": "2026-10-10",
                "startTime": "09:00",
                "endDate": "2026-10-11",
                "endTime": "17:00"
            }
            """;

        var result = DateRecommendationService.ParseResponse(json);

        await Assert.That(result.StartDate).IsEqualTo(new DateOnly(2026, 10, 10));
        await Assert.That(result.EndDate).IsEqualTo(new DateOnly(2026, 10, 11));
        await Assert.That(result.Reasoning).IsNull();
    }

    [Test]
    public async Task ParseResponse_WithMarkdownFencing_StripsAndParses()
    {
        var json = """
            ```json
            {
                "startDate": "2026-11-07",
                "startTime": "14:00",
                "endDate": "2026-11-07",
                "endTime": "17:00",
                "reasoning": "Fenced response"
            }
            ```
            """;

        var result = DateRecommendationService.ParseResponse(json);

        await Assert.That(result.StartDate).IsEqualTo(new DateOnly(2026, 11, 7));
        await Assert.That(result.Reasoning).IsEqualTo("Fenced response");
    }

    [Test]
    public async Task ParseResponse_MalformedJson_ThrowsJsonException()
    {
        var json = "not valid json at all";

        Assert.Throws<System.Text.Json.JsonException>(
            () => DateRecommendationService.ParseResponse(json));

        await Task.CompletedTask;
    }

    [Test]
    public async Task ParseResponse_MissingRequiredField_ThrowsException()
    {
        var json = """
            {
                "startDate": "2026-08-15",
                "startTime": "14:00"
            }
            """;

        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => DateRecommendationService.ParseResponse(json));

        await Task.CompletedTask;
    }

    [Test]
    public async Task ParseResponse_InvalidDateFormat_ThrowsFormatException()
    {
        var json = """
            {
                "startDate": "15/08/2026",
                "startTime": "14:00",
                "endDate": "15/08/2026",
                "endTime": "18:00"
            }
            """;

        Assert.Throws<FormatException>(
            () => DateRecommendationService.ParseResponse(json));

        await Task.CompletedTask;
    }
}
