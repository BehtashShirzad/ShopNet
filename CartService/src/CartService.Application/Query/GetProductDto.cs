using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CartService.Application.Query
{public record GetProductDto(Guid Id,string Name,decimal Price);
}