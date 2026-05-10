using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CartService.Application.Commands;

namespace CartService.Application.Query
{
    public record CartDto(List<ProductDto>Products,decimal TotalPrice);
    
}