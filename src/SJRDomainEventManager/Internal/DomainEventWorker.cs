using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SJRDomainEventManager.PublicFacing.CallMethodsForSetup;

namespace SJRDomainEventManager.Internal;

/// <summary>
/// Drains the domain event outbox in the background.
/// </summary>
public sealed class DomainEventWorker(
    IServiceScopeFactory scopeFactory,
    DomainEventManagerOptions options,
    ILogger<DomainEventWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var processor = scope.ServiceProvider
                    .GetRequiredService<IDomainEventWorkerBatchProcessor>();

                var processedCount =
                    await processor.ProcessBatchAsync(stoppingToken);

                if (processedCount == 0)
                    await Task.Delay(options.PollingDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // Normal application shutdown.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox batch processing failed.");
                await Task.Delay(options.PollingDelay, stoppingToken);
            }
        }
    }
}
