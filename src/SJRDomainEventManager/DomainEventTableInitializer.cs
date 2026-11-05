using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace SJRDomainEventManager;

/// <summary>
/// Creates the <see cref="DomainEvent"/> outbox table when it is missing.
/// </summary>
public static class DomainEventTableInitializer
{
    /// <summary>
    /// Ensures the database behind the given context contains the <see cref="DomainEvent"/> table.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The model of the context does not map <see cref="DomainEvent"/>; call
    /// <see cref="DomainEventModelBuilderExtensions.AddDomainEvents(ModelBuilder, string?)"/> from
    /// <see cref="DbContext.OnModelCreating(ModelBuilder)"/>.
    /// </exception>
    public static void EnsureDomainEventsTable(this DbContext context)
    {
        var commands = GetMissingTableCommands(context);

        foreach (var command in commands)
            context.Database.ExecuteSqlRaw(command);
    }

    /// <inheritdoc cref="EnsureDomainEventsTable(DbContext)"/>
    public static async Task EnsureDomainEventsTableAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        var commands = GetMissingTableCommands(context);

        foreach (var command in commands)
            await context.Database.ExecuteSqlRawAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> GetMissingTableCommands(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var entityType = context.Model.FindEntityType(typeof(DomainEvent))
            ?? throw new InvalidOperationException(
                $"The model of '{context.GetType().Name}' does not contain '{nameof(DomainEvent)}'. " +
                $"Call modelBuilder.{nameof(DomainEventModelBuilderExtensions.AddDomainEvents)}() " +
                "in OnModelCreating.");

        if (TableExists(context))
            return [];

        var tableName = entityType.GetTableName();
        var schema = entityType.GetSchema();

        var designTimeModel = context.GetService<IDesignTimeModel>().Model;

        var operations = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(null, designTimeModel.GetRelationalModel())
            .Where(operation => operation switch
            {
                CreateTableOperation create => create.Name == tableName && create.Schema == schema,
                CreateIndexOperation index => index.Table == tableName && index.Schema == schema,
                _ => false
            })
            .ToList();

        return context.GetService<IMigrationsSqlGenerator>()
            .Generate(operations, designTimeModel)
            .Select(command => command.CommandText)
            .ToList();
    }

    private static bool TableExists(DbContext context)
    {
        try
        {
            context.Set<DomainEvent>().Take(1).Any();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
