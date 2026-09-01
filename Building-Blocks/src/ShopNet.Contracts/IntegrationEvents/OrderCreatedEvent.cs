using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShopNet.Contracts.SharedDtos;

namespace ShopNet.Contracts.IntegrationEvents
{
    public record OrderCreatedEvent(Guid OrderId,Guid CustomerId, List<ProductDto> Items):IntegrationEvent;
    
}