using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Hackmum.Bethuya.Tests.Data;

public sealed class PlannerInvocationAuditMigrationTests
{
    private const string IndexName = "IX_PlannerInvocationAudits_PlanningCycleId_WorkItemId";
    private const string TableName = "PlannerInvocationAudits";

    [Test]
    public async Task MakePlannerInvocationAuditIndexUnique_UpAndDown_ToggleIndexUniquenessOnly()
    {
        var migration = new MakePlannerInvocationAuditIndexUnique();

        await Assert.That(migration.UpOperations.Count).IsEqualTo(2);
        var upDrop = migration.UpOperations[0] as DropIndexOperation;
        var upCreate = migration.UpOperations[1] as CreateIndexOperation;

        await Assert.That(upDrop).IsNotNull();
        await Assert.That(upDrop!.Name).IsEqualTo(IndexName);
        await Assert.That(upDrop.Table).IsEqualTo(TableName);

        await Assert.That(upCreate).IsNotNull();
        await Assert.That(upCreate!.Name).IsEqualTo(IndexName);
        await Assert.That(upCreate.Table).IsEqualTo(TableName);
        await Assert.That(upCreate.Columns).IsEquivalentTo(["PlanningCycleId", "WorkItemId"]);
        await Assert.That(upCreate.IsUnique).IsTrue();

        await Assert.That(migration.DownOperations.Count).IsEqualTo(2);
        var downDrop = migration.DownOperations[0] as DropIndexOperation;
        var downCreate = migration.DownOperations[1] as CreateIndexOperation;

        await Assert.That(downDrop).IsNotNull();
        await Assert.That(downDrop!.Name).IsEqualTo(IndexName);
        await Assert.That(downDrop.Table).IsEqualTo(TableName);

        await Assert.That(downCreate).IsNotNull();
        await Assert.That(downCreate!.Name).IsEqualTo(IndexName);
        await Assert.That(downCreate.Table).IsEqualTo(TableName);
        await Assert.That(downCreate.Columns).IsEquivalentTo(["PlanningCycleId", "WorkItemId"]);
        await Assert.That(downCreate.IsUnique).IsFalse();
    }

    [Test]
    public async Task BethuyaDbContext_HasNoPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=unused")
            .Options;

        await using var dbContext = new BethuyaDbContext(options);

        await Assert.That(dbContext.Database.HasPendingModelChanges()).IsFalse();
    }
}
