using SJRDomainEventManager.PublicFacing.ApplyInterfacesForFunctionality;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SJRDomainEventManager;

/// <summary>
/// Serializes and deserializes domain events stored in the outbox table.
/// </summary>
public static class DomainEventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new BasicDomainEventJsonConverterFactory() }
    };

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

    private sealed class BasicDomainEventJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType)
                return false;

            var genericType = typeToConvert.GetGenericTypeDefinition();
            return genericType == typeof(PublicFacing.BasicAdditionDomainEvent<>)
                || genericType == typeof(PublicFacing.BasicUpdateDomainEvent<>)
                || genericType == typeof(PublicFacing.BasicDeletionDomainEvent<>);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(
                typeof(BasicDomainEventJsonConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class BasicDomainEventJsonConverter<TEvent> : JsonConverter<TEvent>
        where TEvent : IDomainEvent
    {
        public override TEvent Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            var id = root.GetProperty("id").GetGuid();
            var entityType = ResolveRequiredType(root.GetProperty("entityType").GetString(), "entityType");
            var expectedEntityType = typeToConvert.GetGenericArguments()[0];

            if (entityType != expectedEntityType)
                throw new JsonException(
                    $"Entity type '{entityType}' does not match expected type '{expectedEntityType}'.");

            var primaryKeyType = ResolveRequiredType(
                root.GetProperty("primaryKeyType").GetString(),
                "primaryKeyType");
            var primaryKey = root.GetProperty("primaryKey").Deserialize(primaryKeyType, options)
                ?? throw new JsonException("The basic domain event primary key cannot be null.");

            return (TEvent)Activator.CreateInstance(typeToConvert, id, primaryKey, primaryKeyType)!;
        }

        public override void Write(
            Utf8JsonWriter writer,
            TEvent value,
            JsonSerializerOptions options)
        {
            var eventType = value.GetType();
            var primaryKey = eventType.GetProperty("PrimaryKey")!.GetValue(value)
                ?? throw new JsonException("The basic domain event primary key cannot be null.");
            var primaryKeyType = (Type)eventType.GetProperty("PrimaryKeyType")!.GetValue(value)!;
            var entityType = (Type)eventType.GetProperty("EntityType")!.GetValue(value)!;

            writer.WriteStartObject();
            writer.WriteString("id", value.Id);
            writer.WriteString("entityType", GetTypeName(entityType));
            writer.WritePropertyName("primaryKey");
            JsonSerializer.Serialize(writer, primaryKey, primaryKeyType, options);
            writer.WriteString("primaryKeyType", GetTypeName(primaryKeyType));
            writer.WriteEndObject();
        }

        private static Type ResolveRequiredType(string? typeName, string propertyName) =>
            ResolveType(typeName ?? string.Empty)
            ?? throw new JsonException($"Unknown type in '{propertyName}': '{typeName}'.");
    }
}
