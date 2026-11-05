using Microsoft.EntityFrameworkCore;
using SJRDomainEventManager.Internal;
using SJRDomainEventManager.PublicFacing.ApplyInterfacesForFunctionality;
using SJRDomainEventManager.PublicFacing.CallMethodsForSetup;

namespace SJRDomainEventManager.Tests;

public sealed record CustomerRenamed(Guid Id, Guid CustomerId, string Name) : IDomainEvent;

public class Customer : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public void CreateDomainEvent<T>(T Event)
        where T : IDomainEvent => _domainEvents.Add(Event);

    public List<IDomainEvent> GetAllDomainEvents() => _domainEvents;
}

public class OutboxTestContext(DbContextOptions<OutboxTestContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<DatabaseFlattenedDomainEvent> DomainEvents => Set<DatabaseFlattenedDomainEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasKey(customer => customer.Id);
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
