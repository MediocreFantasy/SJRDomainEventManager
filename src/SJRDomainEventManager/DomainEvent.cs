namespace SJRDomainEventManager;

/// <summary>
/// The persisted (outbox) representation of an <see cref="IDomainEvent"/>.
/// </summary>
public class DomainEvent
{
    /// <summary>
    /// The name of the table holding the persisted domain events.
    /// </summary>
    public const string TableName = "DomainEvents";

    /// <summary>
    /// The identifier of the domain event. Matches <see cref="IDomainEvent.Id"/>.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The type of the serialized domain event, used to rehydrate it.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The serialized (JSON) payload of the domain event.
    /// </summary>
    public string JsonedData { get; set; } = string.Empty;

    /// <summary>
    /// The moment at which the domain event was persisted.
    /// </summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>
    /// The moment at which the domain event was successfully handled, if it was.
    /// </summary>
    public DateTime? ProcessedOnUtc { get; set; }

    /// <summary>
    /// The error produced by the last failed handling attempt, if any.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Creates the persisted representation of the given domain event.
    /// </summary>
    public static DomainEvent FromDomainEvent(IDomainEvent Event)
    {
        ArgumentNullException.ThrowIfNull(Event);

        return new DomainEvent
        {
            Id = Event.Id,
            EventType = DomainEventSerializer.GetTypeName(Event.GetType()),
            JsonedData = DomainEventSerializer.Serialize(Event),
            OccurredOnUtc = DateTime.UtcNow
        };
    }
}
