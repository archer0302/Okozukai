using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okozukai.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeFeeAmount",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeToAmount",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeToCurrency",
                table: "Transactions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeFeeAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExchangeToAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExchangeToCurrency",
                table: "Transactions");
        }
    }
}
