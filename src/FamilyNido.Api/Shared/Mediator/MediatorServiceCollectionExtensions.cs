using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyNido.Api.Shared.Mediator;

/// <summary>DI registration helpers for the in-process mediator.</summary>
public static class MediatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMediator"/> plus every concrete
    /// <see cref="IRequestHandler{TRequest, TResponse}"/> implementation
    /// found in <paramref name="assembly"/> as scoped services.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="assembly">Assembly scanned for handler types.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMediator(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IMediator, Mediator>();

        var handlerOpenType = typeof(IRequestHandler<,>);

        var registrations = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces(), (impl, iface) => (Impl: impl, Iface: iface))
            .Where(x => x.Iface.IsGenericType && x.Iface.GetGenericTypeDefinition() == handlerOpenType);

        foreach (var (impl, iface) in registrations)
        {
            services.AddScoped(iface, impl);
        }

        return services;
    }
}
