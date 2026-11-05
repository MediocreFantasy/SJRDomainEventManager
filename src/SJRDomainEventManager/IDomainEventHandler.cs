namespace SJRDomainEventManager;

/// <summary>
/// Handles domain events of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the handled domain event.</typeparam>
public interface IDomainEventHandler<in T>
    where T : IDomainEvent
{
    /// <summary>
    /// Handles the given domain event.
    /// </summary>
    /// <param name="Event">The domain event to handle.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task Handle(T Event, CancellationToken cancellationToken = default);
}
