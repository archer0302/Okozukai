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
