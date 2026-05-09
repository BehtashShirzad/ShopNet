using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CartService.Domain.Exceptions
{
    public class CartExceptionMessages
    {
        public const string InvalidCustomerId = "CustomerId cannot be null or empty.";
        public const string CartItemNotFound = "Cart item not found.";
    }
}