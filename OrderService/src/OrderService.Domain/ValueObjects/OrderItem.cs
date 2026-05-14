using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;
namespace OrderService.Domain.ValueObjects
{
    public class OrderItem  : ValueObject
    {
         public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public decimal Price { get; private set; }
    public int Quantity { get; private set; }

    private OrderItem() { }

    public OrderItem(
        Guid productId,
        string productName,
        decimal price,
        int quantity)
    {
        ProductId = productId;
        ProductName = productName;
        Price = price;
        Quantity = quantity;
    }

        protected override IEnumerable<object> GetMemberValues()
        {
           yield return ProductId;
           yield return ProductName;
           yield return Price;
           yield return Quantity;
        }
    }
}