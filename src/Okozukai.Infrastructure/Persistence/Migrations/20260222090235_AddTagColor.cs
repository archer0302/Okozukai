using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okozukai.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTagColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Tags",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#6366f1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "Tags");
        }
    }
}
