using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "result",
                table: "jobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "result",
                table: "jobs");
        }
    }
}
