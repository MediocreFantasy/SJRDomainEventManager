using SJRDomainEventManager.PublicFacing.ApplyInterfacesForFunctionality;

namespace SJRDomainEventManager.PublicFacing;

/// <summary>
/// Raised automatically when an entity is added.
/// </summary>
/// <typeparam name="T">The type of entity that was added.</typeparam>
/// <param name="Id">The unique identifier of this event.</param>
/// <param name="Entity">The entity that was added.</param>
public sealed record BasicAdditionDomainEvent<T>(Guid Id, T Entity) : IDomainEvent
    where T : IHasDomainEvents;

/// <summary>
/// Raised automatically when an entity is updated.
/// </summary>
/// <typeparam name="T">The type of entity that was updated.</typeparam>
/// <param name="Id">The unique identifier of this event.</param>
/// <param name="Entity">The entity that was updated.</param>
public sealed record BasicUpdateDomainEvent<T>(Guid Id, T Entity) : IDomainEvent
    where T : IHasDomainEvents;

/// <summary>
/// Raised automatically when an entity is deleted.
/// </summary>
/// <typeparam name="T">The type of entity that was deleted.</typeparam>
/// <param name="Id">The unique identifier of this event.</param>
/// <param name="Entity">The entity that was deleted.</param>
public sealed record BasicDeletionDomainEvent<T>(Guid Id, T Entity) : IDomainEvent
    where T : IHasDomainEvents;
