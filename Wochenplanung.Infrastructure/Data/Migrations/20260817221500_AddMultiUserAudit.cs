using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wochenplanung.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddMultiUserAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Schema is applied at runtime by SchemaPatcher so existing SQLite databases
        // that already have Version/ScheduleTemplates from raw SQL do not break.
        // This migration keeps the EF model snapshot in sync for future updates.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
