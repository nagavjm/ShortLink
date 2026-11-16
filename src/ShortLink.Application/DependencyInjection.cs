using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShortLink.Application.Common.Interfaces;
using ShortLink.Application.Services;

namespace ShortLink.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Registers all FluentValidation validators in the Application assembly
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Core use-case service
        services.AddScoped<IUrlShortenerService, UrlShortenerService>();

        return services;
    }
}
