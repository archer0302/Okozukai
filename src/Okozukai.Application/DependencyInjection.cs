using Microsoft.Extensions.DependencyInjection;
using Okozukai.Application.Transactions;

namespace Okozukai.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TransactionService>();
        services.AddScoped<TagService>();
        services.AddScoped<JournalService>();
        return services;
    }
}
