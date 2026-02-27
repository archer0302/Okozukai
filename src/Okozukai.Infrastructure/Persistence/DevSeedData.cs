using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Okozukai.Domain.Transactions;

namespace Okozukai.Infrastructure.Persistence;

public static class DevSeedData
{
    /// <summary>
    /// Seeds realistic sample data for local development.
    /// Only runs when the database contains no transactions.
    /// </summary>
    public static void SeedDevelopmentData(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<OkozukaiDbContext>>();
        var db = sp.GetRequiredService<OkozukaiDbContext>();

        // Only seed relational databases (skip in-memory test provider)
        if (!db.Database.IsRelational())
        {
            return;
        }

        if (db.Transactions.Any())
        {
            logger.LogInformation("--> Seed: skipped (transactions already exist).");
            return;
        }

        logger.LogInformation("--> Seed: populating sample data for development...");

        // --- Clean up any existing journals / tags so we start fresh ---
        db.Tags.RemoveRange(db.Tags);
        db.Journals.RemoveRange(db.Journals);
        db.SaveChanges();

        // --- Tags ---
        var tagFood = Tag.Create("Food & Dining", "#ef4444");
        var tagTransport = Tag.Create("Transport", "#f59e0b");
        var tagEntertainment = Tag.Create("Entertainment", "#8b5cf6");
        var tagUtilities = Tag.Create("Utilities", "#06b6d4");
        var tagShopping = Tag.Create("Shopping", "#ec4899");
        var tagHealth = Tag.Create("Health", "#10b981");
        var tagEducation = Tag.Create("Education", "#6366f1");
        var tagGroceries = Tag.Create("Groceries", "#84cc16");

        db.Tags.AddRange(tagFood, tagTransport, tagEntertainment, tagUtilities,
                         tagShopping, tagHealth, tagEducation, tagGroceries);
        db.SaveChanges();

        // --- Journal ---
        var journal = Journal.Create("Personal Budget", "USD");
        db.Journals.Add(journal);
        db.SaveChanges();

        // --- Helper to build a transaction ---
        Transaction Tx(TransactionType type, decimal amount, int year, int month, int day, string? note, params Tag[] tags)
        {
            var tx = Transaction.Create(
                journal.Id,
                journal.IsClosed,
                type,
                amount,
                new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero),
                note);
            if (tags.Length > 0) tx.SetTags(tags);
            return tx;
        }

        var In = TransactionType.In;
        var Out = TransactionType.Out;

        var transactions = new List<Transaction>();

        // ── Sep 2025 ──────────────────────────────────────────────
        transactions.Add(Tx(In,  4200m,  2025, 9, 1,  "Salary",                     new Tag[0]));
        transactions.Add(Tx(Out, 1350m,  2025, 9, 2,  "Rent",                       tagUtilities));
        transactions.Add(Tx(Out, 85.50m, 2025, 9, 4,  "Weekly groceries",           tagGroceries));
        transactions.Add(Tx(Out, 42m,    2025, 9, 6,  "Sushi dinner",               tagFood));
        transactions.Add(Tx(Out, 55m,    2025, 9, 8,  "Monthly transit pass",       tagTransport));
        transactions.Add(Tx(Out, 12.99m, 2025, 9, 10, "Netflix subscription",       tagEntertainment));
        transactions.Add(Tx(Out, 92m,    2025, 9, 11, "Groceries - Costco",         tagGroceries));
        transactions.Add(Tx(Out, 35m,    2025, 9, 14, "New running shoes",          tagShopping, tagHealth));
        transactions.Add(Tx(Out, 28m,    2025, 9, 18, "Thai takeout",               tagFood));
        transactions.Add(Tx(Out, 120m,   2025, 9, 20, "Electric bill",              tagUtilities));
        transactions.Add(Tx(Out, 15m,    2025, 9, 22, "Uber ride",                  tagTransport));
        transactions.Add(Tx(In,  150m,   2025, 9, 25, "Freelance side gig",         new Tag[0]));
        transactions.Add(Tx(Out, 65m,    2025, 9, 27, "Birthday gift for friend",   tagShopping));

        // ── Oct 2025 ──────────────────────────────────────────────
        transactions.Add(Tx(In,  4200m,  2025, 10, 1,  "Salary",                    new Tag[0]));
        transactions.Add(Tx(Out, 1350m,  2025, 10, 2,  "Rent",                      tagUtilities));
        transactions.Add(Tx(Out, 78m,    2025, 10, 3,  "Weekly groceries",          tagGroceries));
        transactions.Add(Tx(Out, 55m,    2025, 10, 5,  "Monthly transit pass",      tagTransport));
        transactions.Add(Tx(Out, 38m,    2025, 10, 7,  "Ramen night out",           tagFood));
        transactions.Add(Tx(Out, 250m,   2025, 10, 9,  "Online Python course",      tagEducation));
        transactions.Add(Tx(Out, 12.99m, 2025, 10, 10, "Netflix subscription",      tagEntertainment));
        transactions.Add(Tx(Out, 45m,    2025, 10, 12, "Concert tickets",           tagEntertainment));
        transactions.Add(Tx(Out, 95m,    2025, 10, 14, "Groceries - Trader Joe's",  tagGroceries));
        transactions.Add(Tx(Out, 22m,    2025, 10, 16, "Coffee beans & supplies",   tagFood));
        transactions.Add(Tx(Out, 110m,   2025, 10, 18, "Electric bill",             tagUtilities));
        transactions.Add(Tx(Out, 30m,    2025, 10, 20, "Flu medication",            tagHealth));
        transactions.Add(Tx(Out, 18m,    2025, 10, 24, "Uber ride",                 tagTransport));
        transactions.Add(Tx(In,  200m,   2025, 10, 28, "Sold old textbooks",        new Tag[0]));
        transactions.Add(Tx(Out, 75m,    2025, 10, 30, "Halloween costume & decor", tagShopping, tagEntertainment));

        // ── Nov 2025 ──────────────────────────────────────────────
        transactions.Add(Tx(In,  4200m,  2025, 11, 1,  "Salary",                    new Tag[0]));
        transactions.Add(Tx(Out, 1350m,  2025, 11, 2,  "Rent",                      tagUtilities));
        transactions.Add(Tx(Out, 82m,    2025, 11, 3,  "Weekly groceries",          tagGroceries));
        transactions.Add(Tx(Out, 55m,    2025, 11, 5,  "Monthly transit pass",      tagTransport));
        transactions.Add(Tx(Out, 12.99m, 2025, 11, 10, "Netflix subscription",      tagEntertainment));
        transactions.Add(Tx(Out, 65m,    2025, 11, 11, "Italian dinner date",       tagFood));
        transactions.Add(Tx(Out, 340m,   2025, 11, 14, "Black Friday laptop deal",  tagShopping));
        transactions.Add(Tx(Out, 88m,    2025, 11, 16, "Groceries - Whole Foods",   tagGroceries));
        transactions.Add(Tx(Out, 105m,   2025, 11, 18, "Electric bill",             tagUtilities));
        transactions.Add(Tx(Out, 50m,    2025, 11, 20, "Annual flu shot",           tagHealth));
        transactions.Add(Tx(Out, 32m,    2025, 11, 22, "Uber rides (3)",            tagTransport));
        transactions.Add(Tx(Out, 25m,    2025, 11, 24, "Streaming services",        tagEntertainment));
        transactions.Add(Tx(In,  500m,   2025, 11, 26, "Thanksgiving bonus",        new Tag[0]));
        transactions.Add(Tx(Out, 180m,   2025, 11, 28, "Thanksgiving dinner supplies", tagFood, tagGroceries));

        // ── Dec 2025 ──────────────────────────────────────────────
        transactions.Add(Tx(In,  4200m,  2025, 12, 1,  "Salary",                    new Tag[0]));
        transactions.Add(Tx(In,  800m,   2025, 12, 15, "Year-end bonus",            new Tag[0]));
        transactions.Add(Tx(Out, 1350m,  2025, 12, 2,  "Rent",                      tagUtilities));
        transactions.Add(Tx(Out, 90m,    2025, 12, 3,  "Weekly groceries",          tagGroceries));
        transactions.Add(Tx(Out, 55m,    2025, 12, 5,  "Monthly transit pass",      tagTransport));
        transactions.Add(Tx(Out, 12.99m, 2025, 12, 10, "Netflix subscription",      tagEntertainment));
        transactions.Add(Tx(Out, 420m,   2025, 12, 12, "Christmas gifts",           tagShopping));
        transactions.Add(Tx(Out, 58m,    2025, 12, 14, "Holiday party dinner",      tagFood, tagEntertainment));
        transactions.Add(Tx(Out, 115m,   2025, 12, 16, "Groceries for holiday baking", tagGroceries));
        transactions.Add(Tx(Out, 125m,   2025, 12, 18, "Electric bill (winter)",    tagUtilities));
        transactions.Add(Tx(Out, 40m,    2025, 12, 20, "Dentist co-pay",            tagHealth));
        transactions.Add(Tx(Out, 22m,    2025, 12, 22, "Uber to airport",           tagTransport));
        transactions.Add(Tx(Out, 35m,    2025, 12, 28, "New Year's party supplies", tagEntertainment, tagFood));

        // ── Jan 2026 ──────────────────────────────────────────────
        transactions.Add(Tx(In,  4500m,  2026, 1, 1,  "Salary (raise!)",            new Tag[0]));
        transactions.Add(Tx(Out, 1350m,  2026, 1, 2,  "Rent",                       tagUtilities));
        transactions.Add(Tx(Out, 75m,    2026, 1, 4,  "Weekly groceries",           tagGroceries));
        transactions.Add(Tx(Out, 55m,    2026, 1, 5,  "Monthly transit pass",       tagTransport));
        transactions.Add(Tx(Out, 150m,   2026, 1, 8,  "Gym annual membership",     tagHealth));
        transactions.Add(Tx(Out, 12.99m, 2026, 1, 10, "Netflix subscription",       tagEntertainment));
        transactions.Add(Tx(Out, 88m,    2026, 1, 12, "Groceries - Costco",         tagGroceries));
        transactions.Add(Tx(Out, 42m,    2026, 1, 14, "Korean BBQ dinner",          tagFood));
        transactions.Add(Tx(Out, 300m,   2026, 1, 16, "Online UX design course",    tagEducation));
        transactions.Add(Tx(Out, 115m,   2026, 1, 18, "Electric bill",              tagUtilities));
        transactions.Add(Tx(Out, 28m,    2026, 1, 20, "Pharmacy",                   tagHealth));
        transactions.Add(Tx(Out, 95m,    2026, 1, 22, "Winter jacket on sale",      tagShopping));
        transactions.Add(Tx(Out, 18m,    2026, 1, 25, "Uber ride",                  tagTransport));
        transactions.Add(Tx(In,  250m,   2026, 1, 28, "Freelance design work",      new Tag[0]));

        // ── Feb 2026 ──────────────────────────────────────────────
        transactions.Add(Tx(In,  4500m,  2026, 2, 1,  "Salary",                     new Tag[0]));
        transactions.Add(Tx(Out, 1350m,  2026, 2, 2,  "Rent",                       tagUtilities));
        transactions.Add(Tx(Out, 80m,    2026, 2, 3,  "Weekly groceries",           tagGroceries));
        transactions.Add(Tx(Out, 55m,    2026, 2, 5,  "Monthly transit pass",       tagTransport));
        transactions.Add(Tx(Out, 85m,    2026, 2, 8,  "Valentine's dinner",         tagFood, tagEntertainment));
        transactions.Add(Tx(Out, 45m,    2026, 2, 10, "Valentine's gift",           tagShopping));
        transactions.Add(Tx(Out, 12.99m, 2026, 2, 10, "Netflix subscription",       tagEntertainment));
        transactions.Add(Tx(Out, 92m,    2026, 2, 13, "Groceries - Trader Joe's",   tagGroceries));
        transactions.Add(Tx(Out, 110m,   2026, 2, 16, "Electric bill",              tagUtilities));
        transactions.Add(Tx(Out, 35m,    2026, 2, 18, "Brunch with friends",        tagFood));
        transactions.Add(Tx(Out, 20m,    2026, 2, 20, "Book - Clean Architecture",  tagEducation));
        transactions.Add(Tx(Out, 25m,    2026, 2, 22, "Uber rides",                 tagTransport));

        db.Transactions.AddRange(transactions);
        db.SaveChanges();

        logger.LogInformation("--> Seed: created {TagCount} tags, 1 journal, and {TxCount} transactions.",
            8, transactions.Count);
    }
}
