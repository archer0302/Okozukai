using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Okozukai.Infrastructure.Persistence;

namespace Okozukai.IntegrationTests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    static CustomWebApplicationFactory()
    {
        // Program.cs's fail-fast connection-string guard (D-14) runs as a top-level
        // statement, before this factory's ConfigureWebHost override ever executes, so
        // it cannot see the in-memory DbContext swap below. Supply a placeholder value
        // so the guard passes; it is never actually dialed, since ConfigureServices
        // replaces OkozukaiDbContext with an in-memory provider immediately afterward.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__okozukai",
            "Host=localhost;Port=5432;Database=okozukai_test_placeholder;Username=test;Password=test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Aggressively remove any EF Core related registrations
            var efCoreDescriptors = services
                .Where(d => d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true ||
                            d.ServiceType.Name.Contains("DbContext") ||
                            d.ServiceType == typeof(OkozukaiDbContext))
                .ToList();

            foreach (var d in efCoreDescriptors)
            {
                services.Remove(d);
            }
            
            // Add DbContext using an in-memory database for testing
            services.AddDbContext<OkozukaiDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForTesting");
            });
        });

        builder.UseEnvironment("Development");
    }
}
