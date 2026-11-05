using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SJRDomainEventManager.Internal;

namespace SJRDomainEventManager.PublicFacing.CallMethodsForSetup;

/// <summary>
/// Registers the domain event outbox infrastructure. Call this from <c>Program.cs</c>.
/// </summary>
public static class DomainEventManager
{
    /// <summary>
    /// Ensures the given context has the <see cref="DatabaseFlattenedDomainEvent"/> table and registers the
    /// background <see cref="DomainEventWorker"/> draining it.
    /// </summary>
    /// <typeparam name="TContext">The context storing the domain events.</typeparam>
    /// <param name="services">The service collection of the application.</param>
    /// <param name="context">The context whose domain event table is ensured.</param>
    /// <param name="configure">Optionally configures the <see cref="DomainEventManagerOptions"/>.</param>
    public static IServiceCollection RegisterBackgroundWorker<TContext>(
        this IServiceCollection services,
        TContext context,
        Action<DomainEventManagerOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(context);

        context.EnsureDomainEventsTable();

        return services.RegisterBackgroundWorker<TContext>(configure);
    }

    /// <summary>
    /// Registers the background <see cref="DomainEventWorker"/> draining the domain event table of
    /// <typeparamref name="TContext"/>. The table is ensured when the application starts.
    /// </summary>
    /// <typeparam name="TContext">The context storing the domain events.</typeparam>
    /// <param name="services">The service collection of the application.</param>
    /// <param name="configure">Optionally configures the <see cref="DomainEventManagerOptions"/>.</param>
    private static IServiceCollection RegisterBackgroundWorker<TContext>(
        this IServiceCollection services,
        Action<DomainEventManagerOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DomainEventManagerOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddScoped<IDomainEventWorkerBatchProcessor, DomainEventWorkerBatchProcessor<TContext>>();
        services.AddHostedService<DomainEventTableHostedInitializer<TContext>>();
        services.AddHostedService<DomainEventWorker>();

        return services;
    }
}

/// <summary>
/// Ensures the <see cref="DatabaseFlattenedDomainEvent"/> table of <typeparamref name="TContext"/> exists when the
/// application starts.
/// </summary>
/// <typeparam name="TContext">The context storing the domain events.</typeparam>
internal sealed class DomainEventTableHostedInitializer<TContext>(IServiceScopeFactory scopeFactory)
    : IHostedService
    where TContext : DbContext
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        await context.EnsureDomainEventsTableAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
