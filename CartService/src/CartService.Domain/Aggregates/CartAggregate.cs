using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;
using CartService.Domain.Entities;
using CartService.Domain.Exceptions;
using Ardalis.GuardClauses;
using Newtonsoft.Json;

namespace CartService.Domain.Aggregates;

public class CartAggregate : AggregateRoot<Guid>
{
    private CartAggregate()
    {

    }
    [JsonProperty]
    public Guid CustomerId { get; private set; }
   
    private readonly List<CartItem> _items = new();
    public IReadOnlyCollection<CartItem> Items => _items;
    public decimal TotalPrice => _items.Sum(i => i.Price * i.Quantity);


    public static CartAggregate Create(Guid customerId)
    {
        Guard.Against.Default(customerId, nameof(customerId), CartExceptionMessages.InvalidCustomerId);

        var cart = new CartAggregate
        {
            Id = IdGenerator.New(),
            CustomerId = customerId,
            CreatedAt = DateTime.UtcNow
        };


        
        return cart;
    }

    public void AddItem(Guid productId, string productName, decimal price, int quantity)
    {
        var existingItem = _items.FirstOrDefault(x => x.ProductId == productId);

        if (existingItem != null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            var item = CartItem.Create(productId, productName, price, quantity);
            _items.Add(item);
        }

        // RecalculateTotal();
    }

    // void RecalculateTotal()
    // {
    //     TotalPrice = _items.Sum(i => i.Price * i.Quantity);
    // }
    public void RemoveItem(Guid productId)
    {
        var item = _items.FirstOrDefault(x => x.ProductId == productId);

        if (item == null)
            return;

        _items.Remove(item);

        // RecalculateTotal();
    }

    public void ChangeItemQuantity(Guid productId, int quantity)
    {
        var item = _items.FirstOrDefault(x => x.ProductId == productId);

        Guard.Against.Null(item, nameof(item), CartExceptionMessages.CartItemNotFound);

        item.ChangeQuantity(quantity);

        // RecalculateTotal();
    }


}
