# SJRDomainEventManager

A small C# class library implementing the transactional outbox pattern for domain events on top of
EF Core.

## Contents

| Type | Purpose |
| --- | --- |
| `IDomainEvent` | A domain event, identified by a `Guid Id`. |
| `IDomainEventHandler<T>` | Handles a domain event: `Task Handle(T Event, CancellationToken)`. |
| `IHasDomainEvents` | An entity raising domain events: `CreateDomainEvent<T>(T Event)` and `List<IDomainEvent> GetAllDomainEvents()`. |
| `DomainEvent` | The persisted domain event (`Guid Id`, `EventType`, `nvarchar(max) JsonedData`, timestamps, `Error`). |
| `DomainEventManager.RegisterBackgroundWorker` | Ensures the `DomainEvents` table exists and registers the background `OutboxWorker`. |
| `DomainEventCollector.CollectDomainEvents` | Called from `SaveChanges` to store the raised domain events as JSON. |

## Usage

### 1. Raise domain events on your entities

```csharp
public sealed record CustomerRenamed(Guid Id, Guid CustomerId, string Name) : IDomainEvent;

public class Customer : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public void CreateDomainEvent<T>(T Event) where T : IDomainEvent => _domainEvents.Add(Event);

    public List<IDomainEvent> GetAllDomainEvents() => _domainEvents;
}
```

### 2. Map the outbox table and collect the domain events when saving

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.AddDomainEvents();

    public override int SaveChanges()
    {
        this.CollectDomainEvents();

        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        this.CollectDomainEvents();

        return base.SaveChangesAsync(cancellationToken);
    }
}
```

`CollectDomainEvents` takes every tracked `IHasDomainEvents` entity, flattens their
`GetAllDomainEvents()`, serializes each domain event to JSON and adds it to the `DomainEvents`
table, inside the same transaction as the entities themselves. Domain events already stored are
skipped, so repeated saves never duplicate them.

### 3. Register the background worker in `Program.cs`

```csharp
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IDomainEventHandler<CustomerRenamed>, CustomerRenamedHandler>();

// Ensures the DomainEvents table exists, then registers the background OutboxWorker.
builder.Services.RegisterBackgroundWorker<AppDbContext>();
```

Pass a context instance (`RegisterBackgroundWorker(dbContext)`) to create the table immediately
instead of at application start, and the optional `Action<OutboxOptions>` to change the batch size
(default `20`) or the polling delay (default two seconds).

The `OutboxWorker` resolves an `IOutboxBatchProcessor` in a new scope, which reads the unprocessed
domain events, deserializes them and dispatches them to every registered
`IDomainEventHandler<T>`. Successfully handled events are stamped with `ProcessedOnUtc`; failures
are recorded in `Error` and retried on the next pass.

## Building and testing

```bash
dotnet build
dotnet test
```
