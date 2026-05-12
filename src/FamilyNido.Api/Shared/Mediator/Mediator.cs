using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyNido.Api.Shared.Mediator;

/// <summary>
/// Default <see cref="IMediator"/>. Resolves the concrete handler
/// (<see cref="IRequestHandler{TRequest, TResponse}"/>) from the current scope
/// based on the runtime type of the request, caching the reflected
/// <see cref="MethodInfo"/> to avoid repeated lookup costs.
/// </summary>
internal sealed class Mediator : IMediator
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), HandlerInvocation> Cache = new();

    private readonly IServiceProvider _services;

    /// <summary>Primary constructor — takes the ambient provider.</summary>
    public Mediator(IServiceProvider services) => _services = services;

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var invocation = Cache.GetOrAdd(
            (requestType, typeof(TResponse)),
            static key =>
            {
                var handlerType = typeof(IRequestHandler<,>).MakeGenericType(key.RequestType, key.ResponseType);
                var method = handlerType.GetMethod(nameof(IRequestHandler<IRequest<object>, object>.HandleAsync))
                    ?? throw new InvalidOperationException(
                        $"Handler type '{handlerType}' is missing the HandleAsync method.");
                return new HandlerInvocation(handlerType, method);
            });

        var handler = _services.GetRequiredService(invocation.HandlerType);
        return (Task<TResponse>)invocation.Method.Invoke(handler, [request, cancellationToken])!;
    }

    private sealed record HandlerInvocation(Type HandlerType, MethodInfo Method);
}
