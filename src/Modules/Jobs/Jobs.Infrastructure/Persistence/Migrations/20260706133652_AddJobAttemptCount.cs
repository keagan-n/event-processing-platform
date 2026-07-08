using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobAttemptCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "jobs");
        }
    }
}
