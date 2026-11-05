using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SJRDomainEventManager.PublicFacing;
using SJRDomainEventManager.PublicFacing.ApplyInterfacesForFunctionality;
using SJRDomainEventManager.PublicFacing.CallMethodsForSetup;

namespace SJRDomainEventManager.Internal;

/// <summary>
/// Handles the domain events stored in the outbox table of <typeparamref name="TContext"/>
/// with the registered <see cref="IDomainEventHandler{T}"/> implementations.
/// </summary>
/// <typeparam name="TContext">The context storing the domain events.</typeparam>
public sealed class DomainEventWorkerBatchProcessor<TContext>(
    TContext context,
    IServiceProvider serviceProvider,
    DomainEventManagerOptions options,
    ILogger<DomainEventWorkerBatchProcessor<TContext>> logger) : IDomainEventWorkerBatchProcessor
    where TContext : DbContext
{
    private static readonly MethodInfo HandleMethod =
        typeof(DomainEventWorkerBatchProcessor<TContext>)
            .GetMethod(nameof(HandleAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <inheritdoc />
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var batch = await context.Set<DatabaseFlattenedDomainEvent>()
            .Where(stored => stored.ProcessedOnUtc == null)
            .OrderBy(stored => stored.OccurredOnUtc)
            .Take(options.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (batch.Count == 0)
            return 0;

        var processed = 0;

        foreach (var stored in batch)
        {
            try
            {
                await HandleAsync(stored, cancellationToken).ConfigureAwait(false);

                stored.ProcessedOnUtc = DateTime.UtcNow;
                stored.Error = null;
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handling domain event {DomainEventId} failed.", stored.Id);
                stored.Error = ex.ToString();
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return processed;
    }

    private async Task HandleAsync(DatabaseFlattenedDomainEvent stored, CancellationToken cancellationToken)
    {
        var eventType = DomainEventSerializer.ResolveType(stored.EventType)
            ?? throw new InvalidOperationException($"Unknown domain event type '{stored.EventType}'.");

        var domainEvent = DomainEventSerializer.Deserialize(stored.JsonedData, eventType)
            ?? throw new InvalidOperationException($"Domain event '{stored.Id}' could not be deserialized.");

        await (Task)HandleMethod
            .MakeGenericMethod(eventType)
            .Invoke(null, [serviceProvider, domainEvent, cancellationToken])!;
    }

    private static async Task HandleAsync<T>(
        IServiceProvider serviceProvider,
        IDomainEvent domainEvent,
        CancellationToken cancellationToken)
        where T : IDomainEvent
    {
        foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<T>>())
            await handler.Handle((T)domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
