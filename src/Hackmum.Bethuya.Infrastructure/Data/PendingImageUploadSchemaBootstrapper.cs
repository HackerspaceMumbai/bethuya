using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Data;

/// <summary>Bootstraps schema objects that EnsureCreated will not add to an existing database.</summary>
public static class PendingImageUploadSchemaBootstrapper
{
    public static async Task EnsurePendingImageUploadSchemaAsync(this BethuyaDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        const string sql = """
            CREATE TABLE IF NOT EXISTS "PendingImageUploads"
            (
                "PublicId" character varying(512) NOT NULL,
                "DeleteTokenHash" character varying(64) NOT NULL,
                "RequestedAt" timestamp with time zone NOT NULL,
                "AttachedAt" timestamp with time zone NULL,
                "DeletedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_PendingImageUploads" PRIMARY KEY ("PublicId")
            );

            CREATE INDEX IF NOT EXISTS "IX_PendingImageUploads_AttachedAt_DeletedAt_RequestedAt"
                ON "PendingImageUploads" ("AttachedAt", "DeletedAt", "RequestedAt");
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
