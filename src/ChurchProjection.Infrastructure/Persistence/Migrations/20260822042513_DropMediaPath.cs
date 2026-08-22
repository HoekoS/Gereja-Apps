using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchProjection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropMediaPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "path",
                table: "media");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "path",
                table: "media",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
