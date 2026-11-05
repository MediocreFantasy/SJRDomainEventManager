using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SJRDomainEventManager.Internal;

namespace SJRDomainEventManager.PublicFacing.CallMethodsForSetup;

/// <summary>
/// Adds the <see cref="DatabaseFlattenedDomainEvent"/> outbox table to an EF Core model.
/// </summary>
public static class DomainEventModelBuilderExtensions
{
    private const string SqlServerValueGenerationStrategy = "SqlServer:ValueGenerationStrategy";

    /// <summary>
    /// Maps the <see cref="DatabaseFlattenedDomainEvent"/> entity, which holds the serialized domain events.
    /// Call this from <see cref="DbContext.OnModelCreating(ModelBuilder)"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder of the context storing the domain events.</param>
    /// <param name="jsonedDataColumnType">
    /// The column type used for <see cref="DatabaseFlattenedDomainEvent.JsonedData"/>.
    /// Defaults to <c>nvarchar(max)</c> on SQL Server and to the provider default elsewhere.
    /// </param>
    public static ModelBuilder AddDomainEvents(this ModelBuilder modelBuilder, string? jsonedDataColumnType = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        jsonedDataColumnType ??= modelBuilder.Model.FindAnnotation(SqlServerValueGenerationStrategy) is not null
            ? "nvarchar(max)"
            : null;

        modelBuilder.Entity<DatabaseFlattenedDomainEvent>(Configure(jsonedDataColumnType));

        return modelBuilder;
    }

    private static Action<EntityTypeBuilder<DatabaseFlattenedDomainEvent>> Configure(string? jsonedDataColumnType) => builder =>
    {
        builder.ToTable(DatabaseFlattenedDomainEvent.TableName);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.EventType).IsRequired();

        var jsonedData = builder.Property(e => e.JsonedData).IsRequired();
        if (jsonedDataColumnType is not null)
            jsonedData.HasColumnType(jsonedDataColumnType);

        builder.HasIndex(e => e.ProcessedOnUtc);
    };
}
