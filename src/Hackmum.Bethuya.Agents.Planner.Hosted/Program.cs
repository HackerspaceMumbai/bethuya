using System.Globalization;
using Hackmum.Bethuya.Agents.Planner.Hosted;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Planning;

var builder = WebApplication.CreateBuilder(args);

var hostedAgentPort = Environment.GetEnvironmentVariable("DEFAULT_AD_PORT") ?? "8088";
builder.WebHost.UseUrls($"http://+:{hostedAgentPort}");

builder.AddServiceDefaults();

var app = builder.Build();

app.MapPost("/responses", (PlannerResponsesRequest request) =>
{
    var generated = GenerateHybridAgenda(request);
    var schemaErrors = PlanningAgendaValidator.Validate(generated.AgendaJson);
    var consistencyErrors = PlanningAgendaValidator.ValidateMarkdownConsistency(generated.AgendaJson, generated.MarkdownAgenda);

    if (schemaErrors.Count > 0 || consistencyErrors.Count > 0)
    {
        return Results.UnprocessableEntity(new
        {
            error = new
            {
                code = "planner_output_validation_failed",
                message = "Planner generated output that does not satisfy the hybrid contract.",
                details = schemaErrors.Concat(consistencyErrors).ToArray()
            },
            recoveryHint = "Retry with refined constraints or simplify the planning request. Ensure event date/timezone are valid."
        });
    }

    return Results.Ok(new PlannerResponsesSuccess(
        ResponseId: $"resp_{Guid.CreateVersion7():N}",
        ConversationId: request.Conversation ?? $"pc_{Guid.CreateVersion7():N}",
        MarkdownAgenda: generated.MarkdownAgenda,
        AgendaJson: generated.AgendaJson,
        AgentName: "planner-hosted",
        AgentVersion: "v1"));
});

app.MapGet("/liveness", () => Results.Ok("Healthy"));
app.MapGet("/readiness", () => Results.Ok("Ready"));
app.MapDefaultEndpoints();

app.Run();

static PlannerHybridResponse GenerateHybridAgenda(PlannerResponsesRequest request)
{
    var input = request.Input;
    var eventDate = DateOnly.ParseExact(input.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    var timeline = new List<PlanningAgendaBlock>
    {
        new()
        {
            BlockId = "blk-01",
            Start = "09:30",
            End = "10:00",
            Title = "Welcome & Community Check-in",
            Description = "Opening context, safety notes, and session outcomes.",
            Format = "other",
            Speakers = [new() { Name = "Organizer", Role = "host" }],
            Tags = ["opening", "community"]
        },
        new()
        {
            BlockId = "blk-02",
            Start = "10:00",
            End = "11:00",
            Title = "Theme Keynote",
            Description = $"Talk aligned to '{ResolveTheme(input)}' with practical examples.",
            Format = "talk",
            Speakers = [new() { Name = "Suggested Speaker A", Role = "speaker" }],
            Tags = ["keynote", "theme"]
        },
        new()
        {
            BlockId = "blk-03",
            Start = "11:00",
            End = "11:15",
            Title = "Break",
            Description = "Short break for networking and logistics.",
            Format = "break",
            Speakers = [],
            Tags = ["break"]
        },
        new()
        {
            BlockId = "blk-04",
            Start = "11:15",
            End = "12:15",
            Title = "Hands-on Workshop",
            Description = "Guided workshop for attendees to apply keynote concepts.",
            Format = "workshop",
            Speakers = [new() { Name = "Suggested Speaker B", Role = "facilitator" }],
            Tags = ["workshop", "hands-on"]
        },
        new()
        {
            BlockId = "blk-05",
            Start = "12:15",
            End = "12:45",
            Title = "Q&A and Next Actions",
            Description = "Audience Q&A, recap, and explicit follow-up ownership.",
            Format = "panel",
            Speakers = [new() { Name = "Organizer Panel", Role = "panel" }],
            Tags = ["qa", "next-steps"]
        }
    };

    var totalMinutes = timeline.Sum(block =>
    {
        var start = TimeOnly.ParseExact(block.Start, "HH:mm", CultureInfo.InvariantCulture);
        var end = TimeOnly.ParseExact(block.End, "HH:mm", CultureInfo.InvariantCulture);
        return (end - start).TotalMinutes;
    });

    var agendaJson = new PlanningAgendaJson
    {
        AgendaVersion = "1.0",
        Event = new PlanningAgendaEvent
        {
            EventId = input.EventId.ToString(),
            Title = input.Title,
            Date = eventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Timezone = input.Timezone,
            Location = input.Location
        },
        Objectives =
        [
            "Maximize attendance fit with venue/locality constraints.",
            "Balance learning content with networking windows.",
            "Provide explicit ownership for next actions."
        ],
        Constraints = request.Input.Constraints is null
            ? []
            : [request.Input.Constraints],
        Agenda = new PlanningAgendaBody
        {
            TotalDurationMinutes = totalMinutes,
            Blocks = timeline
        },
        Rationale = new PlanningAgendaRationale
        {
            KeyTradeoffs =
            [
                "Longer workshop depth vs shorter networking windows.",
                "Keynote breadth vs practical implementation time."
            ],
            InclusionNotes =
            [
                "Session pacing includes a break and mixed participation formats.",
                "No sensitive trait inference is used in planning recommendations."
            ]
        },
        Risks = new PlanningAgendaRisks
        {
            Items =
            [
                new() { Risk = "Key speaker cancellation", Mitigation = "Keep a standby talk format and backup speaker list." },
                new() { Risk = "Workshop overrun", Mitigation = "Pre-timebox exercises and reserve buffer in Q&A." }
            ]
        },
        NextActions = new PlanningAgendaNextActions
        {
            Items =
            [
                new() { Owner = "human", Action = "Review and edit agenda markdown for local context." },
                new() { Owner = "planner", Action = "Suggest speaker invite shortlist for approved theme." },
                new() { Owner = "other_agent", Action = "Prepare post-event reporting template for recap." }
            ]
        }
    };

    var markdown = BuildMarkdown(agendaJson);
    return new PlannerHybridResponse(markdown, agendaJson);
}

static string BuildMarkdown(PlanningAgendaJson agenda)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine(string.Format(
        CultureInfo.InvariantCulture,
        "# {0} — {1} ({2})",
        agenda.Event.Title,
        agenda.Event.Date,
        agenda.Event.Timezone));
    sb.AppendLine();
    sb.AppendLine("## Timeline");
    sb.AppendLine("| Start | End | Title |");
    sb.AppendLine("| --- | --- | --- |");
    foreach (var block in agenda.Agenda.Blocks)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2} |", block.Start, block.End, block.Title));
    }

    sb.AppendLine();
    sb.AppendLine("## Objectives");
    foreach (var objective in agenda.Objectives)
    {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- {0}", objective));
    }

    sb.AppendLine();
    sb.AppendLine("## Constraints");
    if (agenda.Constraints.Count == 0)
    {
        sb.AppendLine("- None provided.");
    }
    else
    {
        foreach (var constraint in agenda.Constraints)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- {0}", constraint));
        }
    }

    sb.AppendLine();
    sb.AppendLine("## Risks");
    foreach (var risk in agenda.Risks.Items)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **{0}** — {1}", risk.Risk, risk.Mitigation));
    }

    sb.AppendLine();
    sb.AppendLine("## Next Actions");
    foreach (var action in agenda.NextActions.Items)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **{0}**: {1}", action.Owner, action.Action));
    }

    return sb.ToString().TrimEnd();
}

static string ResolveTheme(PlannerHostedInput input)
{
    if (!string.IsNullOrWhiteSpace(input.PriorEventsContext))
    {
        return "community trend continuation";
    }

    if (!string.IsNullOrWhiteSpace(input.Constraints))
    {
        return "constraint-aware practical delivery";
    }

    return "applied engineering for local communities";
}

