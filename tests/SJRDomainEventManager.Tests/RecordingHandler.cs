using SJRDomainEventManager.PublicFacing;
using System.Collections.Concurrent;

namespace SJRDomainEventManager.Tests;

public sealed class RecordingHandler : IDomainEventHandler<CustomerRenamed>
{
    public ConcurrentBag<CustomerRenamed> Handled { get; } = [];

    public Task Handle(CustomerRenamed Event, CancellationToken cancellationToken = default)
    {
        Handled.Add(Event);

        return Task.CompletedTask;
    }
}
