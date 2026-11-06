using SJRDomainEventManager.PublicFacing.ApplyInterfacesForFunctionality;

namespace SJRDomainEventManager.PublicFacing;

/// <summary>
/// Raised automatically when an entity is added.
/// </summary>
/// <typeparam name="T">The type of entity that was added.</typeparam>
/// <param name="Id">The unique identifier of this event.</param>
/// <param name="PrimaryKey">The primary key of the entity that was added.</param>
/// <param name="PrimaryKeyType">The type of the entity's primary key.</param>
public sealed record BasicAdditionDomainEvent<T>(
    Guid Id,
    object PrimaryKey,
    Type PrimaryKeyType) : IDomainEvent
    where T : IHasDomainEvents
{
    /// <summary>
    /// The type of entity that was added.
    /// </summary>
    public Type EntityType => typeof(T);
}

/// <summary>
/// Raised automatically when an entity is updated.
/// </summary>
/// <typeparam name="T">The type of entity that was updated.</typeparam>
/// <param name="Id">The unique identifier of this event.</param>
/// <param name="PrimaryKey">The primary key of the entity that was updated.</param>
/// <param name="PrimaryKeyType">The type of the entity's primary key.</param>
public sealed record BasicUpdateDomainEvent<T>(
    Guid Id,
    object PrimaryKey,
    Type PrimaryKeyType) : IDomainEvent
    where T : IHasDomainEvents
{
    /// <summary>
    /// The type of entity that was updated.
    /// </summary>
    public Type EntityType => typeof(T);
}

/// <summary>
/// Raised automatically when an entity is deleted.
/// </summary>
/// <typeparam name="T">The type of entity that was deleted.</typeparam>
/// <param name="Id">The unique identifier of this event.</param>
/// <param name="PrimaryKey">The primary key of the entity that was deleted.</param>
/// <param name="PrimaryKeyType">The type of the entity's primary key.</param>
public sealed record BasicDeletionDomainEvent<T>(
    Guid Id,
    object PrimaryKey,
    Type PrimaryKeyType) : IDomainEvent
    where T : IHasDomainEvents
{
    /// <summary>
    /// The type of entity that was deleted.
    /// </summary>
    public Type EntityType => typeof(T);
}
