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
    [JsonProperty("Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private List<CartItem> SerializedItems { get => _items; set { _items.Clear(); _items.AddRange(value); } }
    [JsonIgnore]
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();
    public decimal TotalPrice => _items.Sum(i => i.Price * i.Quantity);
    [JsonProperty]
    public bool IsCheckedOut{get;private set;}
    [JsonProperty]
    public Guid? CheckoutEventId { get; private set; }
    [JsonProperty]
    public DateTimeOffset? CheckedOutAtUtc { get; private set; }

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
        EnsureEditable();
        if (productId == Guid.Empty || string.IsNullOrWhiteSpace(productName) || quantity <= 0 ||
            price <= 0 || price > 9999999999999999.99m || decimal.Round(price, 2) != price)
            throw new ArgumentException("Valid product, quantity and decimal(18,2) price are required.");
        var existingItem = _items.FirstOrDefault(x => x.ProductId == productId);

        if (existingItem != null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            if (_items.Count >= 100) throw new ArgumentException("A cart allows at most 100 distinct products.");
            var item = CartItem.Create(productId, productName, price, quantity);
            _items.Add(item);
        }

        
    }

    
    public void RemoveItem(Guid productId)
    {
        EnsureEditable();
        var item = _items.FirstOrDefault(x => x.ProductId == productId);

        if (item == null)
            return;

        _items.Remove(item);

       
    }

    public void ChangeItemQuantity(Guid productId, int quantity)
    {
        EnsureEditable();
        var item = _items.FirstOrDefault(x => x.ProductId == productId);

        Guard.Against.Null(item, nameof(item), CartExceptionMessages.CartItemNotFound);

        item.ChangeQuantity(quantity);

        
    }

    public void Checkout()
        => Checkout(Guid.NewGuid(), DateTimeOffset.UtcNow);

    public void Checkout(Guid eventId, DateTimeOffset now)
    {
        if (IsCheckedOut) return;
        if (_items.Count == 0) throw new InvalidOperationException("Cannot checkout an empty cart.");
        if (eventId == Guid.Empty) throw new ArgumentException("Checkout event ID is required.");
        CheckoutEventId = eventId;
        CheckedOutAtUtc = now.ToUniversalTime();
        IsCheckedOut=true;
    }

    private void EnsureEditable()
    {
        if (IsCheckedOut) throw new InvalidOperationException("A checked-out cart cannot be modified.");
    }


}
