namespace SJRDomainEventManager;

/// <summary>
/// Options controlling how the domain event outbox is drained.
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// The maximum number of domain events handled per batch. Defaults to 20.
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// The delay applied when a batch was empty or failed. Defaults to two seconds.
    /// </summary>
    public TimeSpan PollingDelay { get; set; } = TimeSpan.FromSeconds(2);
}
