using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShopNet.Contracts.SharedDtos
{
    public record CartItemDto(Guid ProductId,
      string  ProductName,
      decimal  Price,
     int   Quantity);
    
}