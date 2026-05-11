using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Newtonsoft.Json;
namespace CartService.Domain.Entities
{
    public class CartItem
    {
        
        private CartItem() { }
    
    [JsonConstructor]
    private CartItem(Guid productId, string productName, decimal price, int quantity)
    {
        ProductId = productId;
        ProductName = productName;
        Price = price;
        Quantity = quantity;
    }
    
        public Guid ProductId { get; private set; }
        public string ProductName { get; private set; } = null!;
        public decimal Price { get; private set; }
        public int Quantity { get; private set; }

        internal static CartItem Create(Guid productId, string productName, decimal price, int quantity)
        {
            Guard.Against.Default(productId, nameof(productId));
            Guard.Against.NullOrEmpty(productName, nameof(productName));
            Guard.Against.NegativeOrZero(price, nameof(price));
            Guard.Against.NegativeOrZero(quantity, nameof(quantity));

            return new CartItem
            {
                ProductId = productId,
                ProductName = productName,
                Price = price,
                Quantity = quantity
            };
        }

        internal void IncreaseQuantity(int quantity)
        {
            Guard.Against.NegativeOrZero(quantity, nameof(quantity));
            Quantity += quantity;
        }

        internal void ChangeQuantity(int quantity)
{
    Guard.Against.NegativeOrZero(quantity);

    Quantity = quantity;
}

    }
}