using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okozukai.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalsPhase5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the Journals table
            migrationBuilder.CreateTable(
                name: "Journals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PrimaryCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journals", x => x.Id);
                });

            // 2. Insert a "Default" journal using the most common currency from existing transactions.
            //    Falls back to 'USD' if no transactions exist.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    default_currency TEXT;
                    default_journal_id UUID := gen_random_uuid();
                BEGIN
                    SELECT "Currency" INTO default_currency
                    FROM "Transactions"
                    WHERE "Type" != 'Exchange'
                    GROUP BY "Currency"
                    ORDER BY COUNT(*) DESC
                    LIMIT 1;

                    IF default_currency IS NULL THEN
                        default_currency := 'USD';
                    END IF;

                    INSERT INTO "Journals" ("Id", "Name", "PrimaryCurrency", "IsClosed", "CreatedAt")
                    VALUES (default_journal_id, 'Default', default_currency, false, NOW());

                    -- 3. Add JournalId column (nullable first so we can backfill)
                    ALTER TABLE "Transactions" ADD COLUMN "JournalId" UUID NULL;

                    -- 4. Backfill all non-Exchange transactions with the default journal
                    UPDATE "Transactions" SET "JournalId" = default_journal_id WHERE "Type" != 'Exchange';

                    -- 5. Delete Exchange-type transactions (they're being removed from the model)
                    DELETE FROM "Transactions" WHERE "Type" = 'Exchange';

                    -- 6. Make JournalId NOT NULL now that Exchange rows are gone
                    ALTER TABLE "Transactions" ALTER COLUMN "JournalId" SET NOT NULL;
                END $$;
                """);

            // 7. Add FK constraint from Transactions to Journals (cascade delete)
            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Journals_JournalId",
                table: "Transactions",
                column: "JournalId",
                principalTable: "Journals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // 8. Add index on JournalId for query performance
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_JournalId",
                table: "Transactions",
                column: "JournalId");

            // 9. Drop exchange columns
            migrationBuilder.DropColumn(name: "ExchangeToAmount", table: "Transactions");
            migrationBuilder.DropColumn(name: "ExchangeToCurrency", table: "Transactions");
            migrationBuilder.DropColumn(name: "ExchangeFeeAmount", table: "Transactions");

            // 10. Drop per-transaction Currency column (now inherited from Journal)
            migrationBuilder.DropColumn(name: "Currency", table: "Transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore exchange and currency columns
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Transactions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

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

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeFeeAmount",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Journals_JournalId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_JournalId",
                table: "Transactions");

            migrationBuilder.DropColumn(name: "JournalId", table: "Transactions");

            migrationBuilder.DropTable(name: "Journals");
        }
    }
}
