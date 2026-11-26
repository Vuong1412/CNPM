using Microsoft.Extensions.DependencyInjection;
using QOL.Application.Accounts;
using QOL.Application.Audio;
using QOL.Application.Commerce;
using QOL.Application.Locations;
using QOL.Application.Payments;
using QOL.Application.Subscriptions;
using QOL.Infrastructure.Notifications;
using QOL.Infrastructure.Payments;
using QOL.Infrastructure.Audio;
using QOL.Infrastructure.Persistence.Services;

namespace QOL.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddQolInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<ISubscriptionAccessService, SubscriptionAccessService>();
        services.AddScoped<IOrderFulfillmentService, OrderFulfillmentService>();
        services.AddScoped<IEmailProvider, FakeEmailProvider>();
        services.AddScoped<IPaymentGateway, DisabledPaymentGateway>();
        services.AddScoped<ITextToSpeechProvider, DisabledTextToSpeechProvider>();
        services.AddScoped<IActionLogService, ActionLogService>();
        return services;
    }
}
