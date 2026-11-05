using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace SJRDomainEventManager.Tests;

public class DomainEventManagerTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Filename=:memory:");

    public DomainEventManagerTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private OutboxTestContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OutboxTestContext>()
            .UseSqlite(_connection)
            .Options;

        return new OutboxTestContext(options);
    }

    private OutboxTestContext CreateInitializedContext()
    {
        var context = CreateContext();
        context.Database.EnsureCreated();

        return context;
    }

    [Fact]
    public void EnsureDomainEventsTable_CreatesTheTableWhenItIsMissing()
    {
        using var context = CreateContext();

        context.EnsureDomainEventsTable();

        Assert.Empty(context.DomainEvents.ToList());
    }

    [Fact]
    public void EnsureDomainEventsTable_IsIdempotent()
    {
        using var context = CreateInitializedContext();

        context.EnsureDomainEventsTable();
        context.EnsureDomainEventsTable();

        Assert.Empty(context.DomainEvents.ToList());
    }

    [Fact]
    public void SaveChanges_StoresTheJsonedDomainEventsOfTrackedEntities()
    {
        using var context = CreateInitializedContext();

        var customer = new Customer { Id = Guid.NewGuid(), Name = "Ada" };
        var domainEvent = new CustomerRenamed(Guid.NewGuid(), customer.Id, "Ada");
        customer.CreateDomainEvent(domainEvent);

        context.Customers.Add(customer);
        context.SaveChanges();

        var stored = Assert.Single(context.DomainEvents.AsNoTracking().ToList());
        Assert.Equal(domainEvent.Id, stored.Id);
        Assert.Equal(DomainEventSerializer.GetTypeName(typeof(CustomerRenamed)), stored.EventType);
        Assert.Null(stored.ProcessedOnUtc);

        using var json = JsonDocument.Parse(stored.JsonedData);
        Assert.Equal("Ada", json.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void SaveChanges_DoesNotStoreTheSameDomainEventTwice()
    {
        using var context = CreateInitializedContext();

        var customer = new Customer { Id = Guid.NewGuid(), Name = "Ada" };
        customer.CreateDomainEvent(new CustomerRenamed(Guid.NewGuid(), customer.Id, "Ada"));

        context.Customers.Add(customer);
        context.SaveChanges();

        customer.Name = "Grace";
        context.SaveChanges();

        Assert.Single(context.DomainEvents.AsNoTracking().ToList());
    }

    [Fact]
    public async Task ProcessBatchAsync_HandlesStoredDomainEvents()
    {
        using var context = CreateInitializedContext();

        var customer = new Customer { Id = Guid.NewGuid(), Name = "Ada" };
        customer.CreateDomainEvent(new CustomerRenamed(Guid.NewGuid(), customer.Id, "Ada"));
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var handler = new RecordingHandler();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IDomainEventHandler<CustomerRenamed>>(handler)
            .AddSingleton(new OutboxOptions())
            .BuildServiceProvider();

        var processor = new OutboxBatchProcessor<OutboxTestContext>(
            context,
            services,
            new OutboxOptions(),
            services.GetRequiredService<ILogger<OutboxBatchProcessor<OutboxTestContext>>>());

        var processed = await processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal("Ada", Assert.Single(handler.Handled).Name);
        Assert.NotNull(context.DomainEvents.AsNoTracking().Single().ProcessedOnUtc);

        Assert.Equal(0, await processor.ProcessBatchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProcessBatchAsync_RecordsTheErrorOfAFailedDomainEvent()
    {
        using var context = CreateInitializedContext();

        context.DomainEvents.Add(new DomainEvent
        {
            Id = Guid.NewGuid(),
            EventType = "Unknown.Event, Unknown",
            JsonedData = "{}",
            OccurredOnUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var services = new ServiceCollection().AddLogging().BuildServiceProvider();

        var processor = new OutboxBatchProcessor<OutboxTestContext>(
            context,
            services,
            new OutboxOptions(),
            services.GetRequiredService<ILogger<OutboxBatchProcessor<OutboxTestContext>>>());

        Assert.Equal(0, await processor.ProcessBatchAsync(CancellationToken.None));

        var stored = context.DomainEvents.AsNoTracking().Single();
        Assert.Null(stored.ProcessedOnUtc);
        Assert.Contains("Unknown.Event", stored.Error);
    }

    [Fact]
    public async Task RegisterBackgroundWorker_DrainsTheOutboxInTheBackground()
    {
        using var context = CreateInitializedContext();

        var customer = new Customer { Id = Guid.NewGuid(), Name = "Ada" };
        customer.CreateDomainEvent(new CustomerRenamed(Guid.NewGuid(), customer.Id, "Ada"));
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var handler = new RecordingHandler();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<OutboxTestContext>(options => options.UseSqlite(_connection));
        builder.Services.AddSingleton<IDomainEventHandler<CustomerRenamed>>(handler);
        builder.Services.RegisterBackgroundWorker<OutboxTestContext>(
            context,
            options => options.PollingDelay = TimeSpan.FromMilliseconds(10));

        using var host = builder.Build();
        await host.StartAsync();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (handler.Handled.IsEmpty && DateTime.UtcNow < deadline)
            await Task.Delay(25);

        await host.StopAsync();

        Assert.Equal("Ada", Assert.Single(handler.Handled).Name);
    }
}
