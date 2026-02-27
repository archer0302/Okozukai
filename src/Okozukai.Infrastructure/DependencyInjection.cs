using Microsoft.Extensions.DependencyInjection;
using Okozukai.Application.Transactions;
using Okozukai.Infrastructure.Persistence;
using Okozukai.Infrastructure.Persistence.Repositories;

namespace Okozukai.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IJournalRepository, JournalRepository>();
        return services;
    }
}
