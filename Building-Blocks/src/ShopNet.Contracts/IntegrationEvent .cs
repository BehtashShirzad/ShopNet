using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShopNet.Contracts.Interfaces;

namespace ShopNet.Contracts
{
    public abstract record IntegrationEvent : IIntegrationEvent
    {
       public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;
    }
}