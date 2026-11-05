# SJRDomainEventManager

A .NET 10 class library implementing the transactional outbox pattern for domain events with
Entity Framework Core.

## Quick start

The application needs three pieces of setup: map and populate the outbox in your `DbContext`,
register the worker in `Program.cs`, and implement domain events and their handlers.

### 1. Configure your `DbContext`

Call `AddDomainEvents()` from `OnModelCreating`, then call
`CollectAndStoreDomainEvents()` before each EF Core save:

```csharp
using Microsoft.EntityFrameworkCore;
using SJRDomainEventManager.PublicFacing.CallMethodsForSetup;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddDomainEvents();
    }

    public override int SaveChanges()
    {
        this.CollectAndStoreDomainEvents();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        this.CollectAndStoreDomainEvents();
        return base.SaveChangesAsync(cancellationToken);
    }
}
```

This stores domain events in the same transaction as the entities that raised them. Duplicate event
IDs are skipped.

### 2. Register the worker in `Program.cs`

Register your EF Core context, event handlers, and the background worker before building the app:

```csharp
using Microsoft.EntityFrameworkCore;
using SJRDomainEventManager.PublicFacing;
using SJRDomainEventManager.PublicFacing.CallMethodsForSetup;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("The DefaultConnection connection string is missing.");

builder.Services.AddDbContext<AppDbContext>(
    options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IDomainEventHandler<CustomerRenamed>, CustomerRenamedHandler>();

var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connectionString)
    .Options;

using var startupContext = new AppDbContext(dbContextOptions);
builder.Services.RegisterBackgroundWorker(startupContext);

var app = builder.Build();
app.Run();
```

`RegisterBackgroundWorker` ensures the `DomainEvents` table exists and registers the hosted worker
that drains it. Worker behavior can be configured during registration:

```csharp
builder.Services.RegisterBackgroundWorker(
    startupContext,
    options =>
    {
        options.BatchSize = 50;
        options.PollingDelay = TimeSpan.FromSeconds(5);
    });
```

The defaults are 20 events per batch and a two-second polling delay.

### 3. Create and raise a domain event

Domain events implement `IDomainEvent`, while entities that raise them implement
`IHasDomainEvents`:

```csharp
using SJRDomainEventManager.PublicFacing.ApplyInterfacesForFunctionality;

public sealed record CustomerRenamed(Guid Id, Guid CustomerId, string Name) : IDomainEvent;

public class Customer : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public void Rename(string name)
    {
        Name = name;
        CreateDomainEvent(new CustomerRenamed(Guid.NewGuid(), Id, name));
    }

    public void CreateDomainEvent<T>(T Event) where T : IDomainEvent =>
        _domainEvents.Add(Event);

    public List<IDomainEvent> GetAllDomainEvents() => _domainEvents;
}
```

### 4. Handle the domain event

Implement `IDomainEventHandler<T>` and register the handler in `Program.cs`, as shown above:

```csharp
using SJRDomainEventManager.PublicFacing;

public sealed class CustomerRenamedHandler : IDomainEventHandler<CustomerRenamed>
{
    public Task Handle(
        CustomerRenamed Event,
        CancellationToken cancellationToken = default)
    {
        // Perform the side effect for the event.
        return Task.CompletedTask;
    }
}
```

Successfully handled events receive a `ProcessedOnUtc` timestamp. Failures are written to `Error`
and retried on the next worker pass.

## Project structure

```text
SJRDomainEventManager.slnx
├── src/
│   └── SJRDomainEventManager/
│       ├── PublicFacing/
│       │   ├── ApplyInterfacesForFunctionality/
│       │   └── CallMethodsForSetup/
│       └── Internal/
└── tests/
    └── SJRDomainEventManager.Tests/
```

- `PublicFacing/ApplyInterfacesForFunctionality` contains the interfaces used by application domain
  events, entities, and handlers.
- `PublicFacing/CallMethodsForSetup` contains the EF Core model, save, and worker registration
  extensions used during application setup.
- `Internal` contains the persisted event model, serializer, table initializer, and background
  processing implementation.
- `tests/SJRDomainEventManager.Tests` contains the xUnit integration tests.

## Public API

| API | Purpose |
| --- | --- |
| `IDomainEvent` | Defines a domain event with a unique `Guid Id`. |
| `IHasDomainEvents` | Exposes the domain events raised by an entity. |
| `IDomainEventHandler<T>` | Handles domain events of type `T`. |
| `ModelBuilder.AddDomainEvents` | Maps the `DomainEvents` outbox table. |
| `DbContext.CollectAndStoreDomainEvents` | Adds raised domain events to the current EF Core transaction. |
| `IServiceCollection.RegisterBackgroundWorker` | Ensures the outbox table exists and registers its worker. |

## Build and test

```bash
dotnet build
dotnet test
```
