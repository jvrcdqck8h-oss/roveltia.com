using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roveltia.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairUnsubscribeTokenSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('WaitlistSignups', 'UnsubscribeToken') IS NULL
                BEGIN
                    ALTER TABLE [WaitlistSignups]
                    ADD [UnsubscribeToken] nvarchar(64) NULL;
                END
                """);

            migrationBuilder.Sql("""
                UPDATE [WaitlistSignups]
                SET [UnsubscribeToken] = LOWER(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''))
                WHERE [UnsubscribeToken] IS NULL OR [UnsubscribeToken] = '';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE [WaitlistSignups]
                ALTER COLUMN [UnsubscribeToken] nvarchar(64) NOT NULL;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_WaitlistSignups_UnsubscribeToken'
                      AND object_id = OBJECT_ID(N'[WaitlistSignups]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_WaitlistSignups_UnsubscribeToken]
                    ON [WaitlistSignups]([UnsubscribeToken]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_WaitlistSignups_UnsubscribeToken'
                      AND object_id = OBJECT_ID(N'[WaitlistSignups]')
                )
                BEGIN
                    DROP INDEX [IX_WaitlistSignups_UnsubscribeToken] ON [WaitlistSignups];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('WaitlistSignups', 'UnsubscribeToken') IS NOT NULL
                BEGIN
                    ALTER TABLE [WaitlistSignups]
                    DROP COLUMN [UnsubscribeToken];
                END
                """);
        }
    }
}
