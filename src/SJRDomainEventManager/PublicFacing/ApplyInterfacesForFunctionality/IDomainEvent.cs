namespace SJRDomainEventManager.PublicFacing.ApplyInterfacesForFunctionality;

/// <summary>
/// A domain event. Every domain event is uniquely identified by a <see cref="Guid"/>.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// The unique identifier of this domain event.
    /// </summary>
    Guid Id { get; }
}
