using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CartService.Application.Commands;

namespace CartService.Application.Query
{
    public record CartDto(List<ProductViewModelOutput>Products,decimal TotalPrice)
    {
        public bool IsCheckedOut { get; init; }
        public Guid? CheckoutEventId { get; init; }
    }
    
}
