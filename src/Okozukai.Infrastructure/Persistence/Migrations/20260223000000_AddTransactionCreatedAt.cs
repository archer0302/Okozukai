using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okozukai.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Transactions");
        }
    }
}
