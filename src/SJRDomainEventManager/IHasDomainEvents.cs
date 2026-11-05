namespace SJRDomainEventManager;

/// <summary>
/// An entity that raises domain events.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>
    /// Raises the given domain event on this entity.
    /// </summary>
    /// <typeparam name="T">The type of the domain event.</typeparam>
    /// <param name="Event">The domain event to raise.</param>
    void CreateDomainEvent<T>(T Event)
        where T : IDomainEvent;

    /// <summary>
    /// Gets every domain event currently raised on this entity.
    /// </summary>
    List<IDomainEvent> GetAllDomainEvents();
}
