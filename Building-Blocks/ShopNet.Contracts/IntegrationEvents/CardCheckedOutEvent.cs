using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShopNet.Contracts.Interfaces;
using ShopNet.Contracts.SharedDtos;
namespace ShopNet.Contracts.IntegrationEvents
{
   public record CartCheckedOutEvent(
    Guid CartId,
    Guid CustomerId,
    List<CartItemDto> Items,
    decimal TotalPrice
):IntegrationEvent;


    
}