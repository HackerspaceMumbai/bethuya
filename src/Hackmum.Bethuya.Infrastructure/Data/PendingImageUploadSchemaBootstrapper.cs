using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Data;

/// <summary>Bootstraps schema objects that EnsureCreated will not add to an existing database.</summary>
public static class PendingImageUploadSchemaBootstrapper
{
    public static async Task EnsurePendingImageUploadSchemaAsync(this BethuyaDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            IF OBJECT_ID(N'[PendingImageUploads]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PendingImageUploads]
                (
                    [PublicId] nvarchar(512) NOT NULL,
                    [DeleteTokenHash] nvarchar(64) NOT NULL,
                    [RequestedAt] datetimeoffset NOT NULL,
                    [AttachedAt] datetimeoffset NULL,
                    [DeletedAt] datetimeoffset NULL,
                    CONSTRAINT [PK_PendingImageUploads] PRIMARY KEY ([PublicId])
                );
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_PendingImageUploads_AttachedAt_DeletedAt_RequestedAt'
                    AND object_id = OBJECT_ID(N'[PendingImageUploads]'))
            BEGIN
                CREATE INDEX [IX_PendingImageUploads_AttachedAt_DeletedAt_RequestedAt]
                    ON [PendingImageUploads] ([AttachedAt], [DeletedAt], [RequestedAt]);
            END;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
