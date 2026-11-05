namespace SJRDomainEventManager;

/// <summary>
/// Handles a batch of domain events stored in the outbox table.
/// </summary>
public interface IOutboxBatchProcessor
{
    /// <summary>
    /// Handles the next batch of unprocessed domain events.
    /// </summary>
    /// <returns>The number of domain events processed.</returns>
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default);
}
