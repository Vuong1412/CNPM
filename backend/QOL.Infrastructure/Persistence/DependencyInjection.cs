using Microsoft.Extensions.DependencyInjection;
using QOL.Application.Accounts;
using QOL.Application.Locations;
using QOL.Application.Subscriptions;
using QOL.Infrastructure.Persistence.Services;

namespace QOL.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddQolInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<ISubscriptionAccessService, SubscriptionAccessService>();
        return services;
    }
}
