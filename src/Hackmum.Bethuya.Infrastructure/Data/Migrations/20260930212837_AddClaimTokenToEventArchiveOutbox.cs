using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimTokenToEventArchiveOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reconcile databases created while the historical outbox migration incorrectly included this column.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'EventArchiveOutboxMessages'
                          AND column_name = 'ClaimToken'
                    ) THEN
                        ALTER TABLE "EventArchiveOutboxMessages"
                            ADD COLUMN "ClaimToken" character varying(32);
                    END IF;
                END
                $$;

                UPDATE "EventArchiveOutboxMessages"
                SET "ClaimToken" = ''
                WHERE "ClaimToken" IS NULL;

                ALTER TABLE "EventArchiveOutboxMessages"
                    ALTER COLUMN "ClaimToken" TYPE character varying(32)
                        USING LEFT("ClaimToken", 32),
                    ALTER COLUMN "ClaimToken" SET NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimToken",
                table: "EventArchiveOutboxMessages");
        }
    }
}
