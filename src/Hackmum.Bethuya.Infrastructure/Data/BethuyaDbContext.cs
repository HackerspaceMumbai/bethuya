using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Data;

public sealed class BethuyaDbContext(DbContextOptions<BethuyaDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Agenda> Agendas => Set<Agenda>();
    public DbSet<AgendaSession> AgendaSessions => Set<AgendaSession>();
    public DbSet<AttendanceProposal> AttendanceProposals => Set<AttendanceProposal>();
    public DbSet<WaitlistProposal> WaitlistProposals => Set<WaitlistProposal>();
    public DbSet<CurationInsights> CurationInsights => Set<CurationInsights>();
    public DbSet<FairnessBudget> FairnessBudgets => Set<FairnessBudget>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<EventReport> EventReports => Set<EventReport>();
    public DbSet<AttendeeProfile> AttendeeProfiles => Set<AttendeeProfile>();
    public DbSet<PlanningCycle> PlanningCycles => Set<PlanningCycle>();
    public DbSet<PlannerDraft> PlannerDrafts => Set<PlannerDraft>();
    public DbSet<PlannerInvocationAudit> PlannerInvocationAudits => Set<PlannerInvocationAudit>();
    public DbSet<PublishedScheduleSnapshot> PublishedScheduleSnapshots => Set<PublishedScheduleSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BethuyaDbContext).Assembly);
    }
}
