using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CartService.Application.Commands
{
      public record ProductDto(Guid ProductId,int Quantity,decimal Price,string ProductName);
}