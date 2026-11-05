using System.Text.Json;

namespace SJRDomainEventManager;

/// <summary>
/// Serializes and deserializes domain events stored in the outbox table.
/// </summary>
public static class DomainEventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes the given domain event to JSON.
    /// </summary>
    public static string Serialize(IDomainEvent Event)
    {
        ArgumentNullException.ThrowIfNull(Event);

        return JsonSerializer.Serialize(Event, Event.GetType(), Options);
    }

    /// <summary>
    /// Deserializes the given JSON payload into a domain event of the given type.
    /// </summary>
    public static IDomainEvent? Deserialize(string json, Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return JsonSerializer.Deserialize(json, eventType, Options) as IDomainEvent;
    }

    /// <summary>
    /// Gets the assembly qualified (but version independent) name used to persist the type of a domain event.
    /// </summary>
    public static string GetTypeName(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return $"{eventType.FullName}, {eventType.Assembly.GetName().Name}";
    }

    /// <summary>
    /// Resolves the type previously persisted by <see cref="GetTypeName(Type)"/>,
    /// or <see langword="null"/> when it cannot be found.
    /// </summary>
    public static Type? ResolveType(string eventType) =>
        string.IsNullOrWhiteSpace(eventType) ? null : Type.GetType(eventType, throwOnError: false);
}
