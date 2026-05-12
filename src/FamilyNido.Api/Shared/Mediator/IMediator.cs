namespace FamilyNido.Api.Shared.Mediator;

/// <summary>
/// Marker for a request (command or query) that expects a response of
/// <typeparamref name="TResponse"/>. Handlers are discovered via DI through
/// <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">Type returned by the matching handler.</typeparam>
#pragma warning disable CA1040 // Marker interface is intentional: acts as the typing link between request and handler.
public interface IRequest<TResponse>
{
}
#pragma warning restore CA1040

/// <summary>
/// Handler for a specific <see cref="IRequest{TResponse}"/>. One handler per
/// request type is the expected shape.
/// </summary>
/// <typeparam name="TRequest">Request this handler consumes.</typeparam>
/// <typeparam name="TResponse">Response this handler produces.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Handles the request and returns its response.</summary>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches requests to their registered <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// The implementation resolves the handler from <see cref="IServiceProvider"/> on
/// each call, so handlers are effectively scoped to the current DI scope.
/// </summary>
public interface IMediator
{
    /// <summary>Send the request and await its handler response.</summary>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken);
}
