using Microsoft.EntityFrameworkCore;
using SJRDomainEventManager.Internal;
using SJRDomainEventManager.PublicFacing.ApplyInterfacesForFunctionality;

namespace SJRDomainEventManager.PublicFacing.CallMethodsForSetup;

/// <summary>
/// Collects the domain events raised by the tracked entities and stores them in the outbox table.
/// </summary>
public static class DomainEventCollector
{
    /// <summary>
    /// Serializes every domain event raised by the tracked <see cref="IHasDomainEvents"/> entities
    /// and adds it to the <see cref="DatabaseFlattenedDomainEvent"/> table of the context.
    /// Call this at the beginning of <see cref="DbContext.SaveChanges()"/>, so the domain events are
    /// persisted in the same transaction as the entities raising them.
    /// </summary>
    /// <param name="context">The context saving the entities raising the domain events.</param>
    /// <returns>The domain events added to the context.</returns>
    public static IReadOnlyList<DatabaseFlattenedDomainEvent> CollectAndStoreDomainEvents(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var raised = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .SelectMany(entity => entity.GetAllDomainEvents())
            .Where(domainEvent => domainEvent is not null)
            .GroupBy(domainEvent => domainEvent.Id)
            .Select(group => group.First())
            .ToList();

        if (raised.Count == 0)
            return [];

        var alreadyStored = context.ChangeTracker
            .Entries<DatabaseFlattenedDomainEvent>()
            .Select(entry => entry.Entity.Id)
            .ToHashSet();

        var ids = raised.Select(domainEvent => domainEvent.Id).ToList();

        foreach (var id in context.Set<DatabaseFlattenedDomainEvent>().Where(stored => ids.Contains(stored.Id)).Select(stored => stored.Id))
            alreadyStored.Add(id);

        var collected = raised
            .Where(domainEvent => !alreadyStored.Contains(domainEvent.Id))
            .Select(DatabaseFlattenedDomainEvent.FromDomainEvent)
            .ToList();

        if (collected.Count > 0)
            context.Set<DatabaseFlattenedDomainEvent>().AddRange(collected);

        return collected;
    }
}
