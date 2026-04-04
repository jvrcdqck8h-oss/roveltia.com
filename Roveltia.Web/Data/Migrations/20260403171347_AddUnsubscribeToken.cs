using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roveltia.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnsubscribeToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnsubscribeToken",
                table: "WaitlistSignups",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE WaitlistSignups
                SET UnsubscribeToken = LOWER(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''))
                WHERE UnsubscribeToken IS NULL OR UnsubscribeToken = ''
                """);

            migrationBuilder.AlterColumn<string>(
                name: "UnsubscribeToken",
                table: "WaitlistSignups",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistSignups_UnsubscribeToken",
                table: "WaitlistSignups",
                column: "UnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaitlistSignups_UnsubscribeToken",
                table: "WaitlistSignups");

            migrationBuilder.DropColumn(
                name: "UnsubscribeToken",
                table: "WaitlistSignups");
        }
    }
}
