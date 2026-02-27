using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Okozukai.Infrastructure.Persistence;

public static class MigrationExtensions
{
    public static void ApplyDatabaseMigrations(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var logger = serviceProvider.GetRequiredService<ILogger<OkozukaiDbContext>>();
        var dbContext = serviceProvider.GetRequiredService<OkozukaiDbContext>();

        var retries = 10;
        while (retries > 0)
        {
            try
            {
                if (!dbContext.Database.IsRelational())
                {
                    logger.LogInformation("--> Skip database migrations because the database provider is not relational.");
                    break;
                }

                logger.LogInformation("--> Attempting to apply database migrations... (Retries left: {Retries})", retries);
                dbContext.Database.Migrate();
                logger.LogInformation("--> Migrations applied successfully.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "--> Migration failed.");

                // Attempt to create the database if it doesn't exist (e.g. initial run with persistent volume)
                if (ex.Message.Contains("3D000") || ex.Message.Contains("does not exist"))
                {
                    try
                    {
                        logger.LogInformation("--> Attempting to create database...");
                        var databaseCreator = dbContext.GetService<IRelationalDatabaseCreator>();
                        databaseCreator.Create();
                        logger.LogInformation("--> Database created successfully. Retrying migration...");
                        continue; // Retry migration immediately
                    }
                    catch (Exception createEx)
                    {
                        logger.LogError(createEx, "--> Database creation failed.");
                    }
                }

                retries--;
                if (retries == 0) throw;
                Thread.Sleep(3000);
            }
        }
    }
}
