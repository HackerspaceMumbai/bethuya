using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendeeProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MobileNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    GovernmentPhotoIdType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GovernmentIdLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    OccupationStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EducationInstitute = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LinkedInMemberId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LinkedInProfileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GitHubLogin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GitHubProfileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsProfileComplete = table.Column<bool>(type: "boolean", nullable: false),
                    ProfileCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsAideProfileComplete = table.Column<bool>(type: "boolean", nullable: false),
                    AideProfileCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GenderIdentity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SelfDescribeGender = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgeRange = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Ethnicity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SelfDescribeEthnicity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Disability = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisabilityDetails = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DietaryRequirements = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LgbtqIdentity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ParentalStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Religion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Caste = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Neighborhood = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModeOfTransportation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SocioeconomicBackground = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Neurodiversity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CaregivingResponsibilities = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LanguageProficiency = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EducationalBackground = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HowDidYouHear = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AdditionalSupport = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendeeProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecidedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Diff = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Hashtag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FairnessTargets = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingImageUploads",
                columns: table => new
                {
                    PublicId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DeleteTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttachedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingImageUploads", x => x.PublicId);
                });

            migrationBuilder.CreateTable(
                name: "PublishedScheduleSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanningCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannerDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarkdownAgenda = table.Column<string>(type: "text", nullable: false),
                    AgendaJson = table.Column<string>(type: "text", nullable: false),
                    PublishedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AgentVersionTag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishedScheduleSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Agendas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByAgent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agendas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agendas_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedAttendeeIds = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceProposals_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Summary = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Highlights = table.Column<string>(type: "text", nullable: false),
                    ActionItems = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DraftedByAgent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EditedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventReports_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActiveDraftId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningCycles_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Registrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Interests = table.Column<string>(type: "text", nullable: false),
                    Intent = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Goals = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContributionPreferences = table.Column<string>(type: "text", nullable: false),
                    ExperienceLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AttendanceLikelihood = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TravelRequirement = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DietaryRequirements = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AccessibilityNeeds = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GovernmentIdFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    GovernmentIdContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GovernmentIdProtectedPayload = table.Column<string>(type: "text", nullable: true),
                    GovernmentIdUploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InclusionSignals = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Registrations_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaitlistProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaitlistedRegistrationIds = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitlistProposals_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgendaSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgendaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Speaker = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendaSessions_Agendas_AgendaId",
                        column: x => x.AgendaId,
                        principalTable: "Agendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CurationInsights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThemeAlignmentScores = table.Column<string>(type: "text", nullable: false),
                    DEINudges = table.Column<string>(type: "text", nullable: false),
                    OverRepresentationAlerts = table.Column<string>(type: "text", nullable: false),
                    CommunitySignals = table.Column<string>(type: "text", nullable: false),
                    FirstComeSignals = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurationInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurationInsights_AttendanceProposals_AttendanceProposalId",
                        column: x => x.AttendanceProposalId,
                        principalTable: "AttendanceProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FairnessBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiversityTargets = table.Column<string>(type: "text", nullable: false),
                    EquityPrompts = table.Column<string>(type: "text", nullable: false),
                    ActualMetrics = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FairnessBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FairnessBudgets_AttendanceProposals_AttendanceProposalId",
                        column: x => x.AttendanceProposalId,
                        principalTable: "AttendanceProposals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FairnessBudgets_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannerDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanningCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MarkdownAgenda = table.Column<string>(type: "text", nullable: false),
                    AgendaJson = table.Column<string>(type: "text", nullable: false),
                    HumanEditedMarkdown = table.Column<string>(type: "text", nullable: true),
                    HumanDiff = table.Column<string>(type: "text", nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalDecision = table.Column<string>(type: "text", nullable: true),
                    ResponseId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgentVersionTag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TraceParent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannerDrafts_PlanningCycles_PlanningCycleId",
                        column: x => x.PlanningCycleId,
                        principalTable: "PlanningCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannerInvocationAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanningCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConversationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResponseId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgentVersionTag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MarkdownAgenda = table.Column<string>(type: "text", nullable: false),
                    AgendaJson = table.Column<string>(type: "text", nullable: false),
                    TraceParent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerInvocationAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannerInvocationAudits_PlanningCycles_PlanningCycleId",
                        column: x => x.PlanningCycleId,
                        principalTable: "PlanningCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agendas_EventId",
                table: "Agendas",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgendaSessions_AgendaId",
                table: "AgendaSessions",
                column: "AgendaId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceProposals_EventId",
                table: "AttendanceProposals",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendeeProfiles_UserId",
                table: "AttendeeProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurationInsights_AttendanceProposalId",
                table: "CurationInsights",
                column: "AttendanceProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventReports_EventId",
                table: "EventReports",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Hashtag",
                table: "Events",
                column: "Hashtag",
                unique: true,
                filter: "\"Hashtag\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FairnessBudgets_AttendanceProposalId",
                table: "FairnessBudgets",
                column: "AttendanceProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FairnessBudgets_EventId",
                table: "FairnessBudgets",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingImageUploads_AttachedAt_DeletedAt_RequestedAt",
                table: "PendingImageUploads",
                columns: new[] { "AttachedAt", "DeletedAt", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannerDrafts_PlanningCycleId_WorkItemId",
                table: "PlannerDrafts",
                columns: new[] { "PlanningCycleId", "WorkItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannerInvocationAudits_PlanningCycleId_WorkItemId",
                table: "PlannerInvocationAudits",
                columns: new[] { "PlanningCycleId", "WorkItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningCycles_EventId_ConversationId",
                table: "PlanningCycles",
                columns: new[] { "EventId", "ConversationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningCycles_EventId_Status",
                table: "PlanningCycles",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PublishedScheduleSnapshots_EventId_PublishedAt",
                table: "PublishedScheduleSnapshots",
                columns: new[] { "EventId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PublishedScheduleSnapshots_PlanningCycleId_PublishedAt",
                table: "PublishedScheduleSnapshots",
                columns: new[] { "PlanningCycleId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_EventId",
                table: "Registrations",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistProposals_EventId",
                table: "WaitlistProposals",
                column: "EventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgendaSessions");

            migrationBuilder.DropTable(
                name: "AttendeeProfiles");

            migrationBuilder.DropTable(
                name: "CurationInsights");

            migrationBuilder.DropTable(
                name: "Decisions");

            migrationBuilder.DropTable(
                name: "EventReports");

            migrationBuilder.DropTable(
                name: "FairnessBudgets");

            migrationBuilder.DropTable(
                name: "PendingImageUploads");

            migrationBuilder.DropTable(
                name: "PlannerDrafts");

            migrationBuilder.DropTable(
                name: "PlannerInvocationAudits");

            migrationBuilder.DropTable(
                name: "PublishedScheduleSnapshots");

            migrationBuilder.DropTable(
                name: "Registrations");

            migrationBuilder.DropTable(
                name: "WaitlistProposals");

            migrationBuilder.DropTable(
                name: "Agendas");

            migrationBuilder.DropTable(
                name: "AttendanceProposals");

            migrationBuilder.DropTable(
                name: "PlanningCycles");

            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
