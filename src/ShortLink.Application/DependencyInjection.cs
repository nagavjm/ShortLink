using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ShortLink.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Registers all FluentValidation validators in the Application assembly
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
