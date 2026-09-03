using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Domain.Abstractions;
using OrderService.Domain.DomanEvents;
using OrderService.Domain.Enums;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Aggregates
{
    public class OrderAggregate:AggregateRoot<Guid>
    {
         public Guid CustomerId{get;init;}
        private readonly List<OrderItem> _items = new();
        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
        public decimal TotalPrice => _items.Sum(x=>x.Price * x.Quantity);
        public OrderStatus Status{get; private set;}
        public Guid CartId { get; private set; }
        public Guid? InventoryReservationRequestId { get; private set; }
        public DateTimeOffset? InventoryReservationExpiresAtUtc { get; private set; }
        public long InventoryReservationVersion { get; private set; }
        public OrderInventoryStatus? InventoryStatus { get; private set; }
        public string? InventoryFailureReason { get; private set; }
        private OrderAggregate()
        {
            
        }
        
       

        public static OrderAggregate Create(Guid customerId, Guid cartId)
        {
            Guard.Against.Default(customerId, nameof(customerId));
            Guard.Against.Default(cartId, nameof(cartId));

            var order =  new OrderAggregate()
            {
             Id=IdGenerator.New(),
             Status = OrderStatus.Pending,  
             CustomerId = customerId,
             CartId = cartId
            };
            return order;
        }

       public void AddItem(Guid productId, string name, decimal price, int quantity)
        {
            if (Status != OrderStatus.Pending || InventoryReservationRequestId.HasValue)
                throw new InvalidOperationException("Cannot modify order");

            if (productId == Guid.Empty || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Product ID and name are required.");
            if (_items.Count >= 100 || _items.Any(x => x.ProductId == productId))
                throw new ArgumentException("An order allows at most 100 distinct products.");
            if (price > 9999999999999999.99m || decimal.Round(price, 2) != price)
                throw new ArgumentException("Price must fit decimal(18,2).");

          
                
            Guard.Against.NullOrOutOfRange(quantity,nameof(quantity), 1, int.MaxValue, "Quantity must be greater than zero");
              

            
            Guard.Against.NullOrOutOfRange(price,nameof(price), 0.01m, decimal.MaxValue, "Price must be greater than zero");

            _items.Add(new OrderItem(productId, name, price, quantity));
        }

        public void BeginInventoryReservation(Guid requestId, DateTimeOffset now, TimeSpan duration)
        {
            if (requestId == Guid.Empty || duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(24))
                throw new ArgumentException("A request ID and a reservation duration up to 24 hours are required.");
            if (InventoryReservationRequestId.HasValue || Status != OrderStatus.Pending || _items.Count == 0)
                throw new InvalidOperationException("Inventory reservation can only start once for a non-empty pending order.");
            InventoryReservationExpiresAtUtc = now.ToUniversalTime().Add(duration);
            InventoryReservationRequestId = requestId;
            InventoryStatus = OrderInventoryStatus.Requested;
        }

        public bool ApplyInventoryResult(Guid requestId, long version, OrderInventoryStatus result, string? reason = null)
        {
            if (requestId != InventoryReservationRequestId) return false;
            if (version <= 0 || !Enum.IsDefined(result) || result == OrderInventoryStatus.Requested)
                throw new ArgumentException("Invalid inventory result/version.");
            if (version <= InventoryReservationVersion) return false;
            // Never resurrect a terminal order, even if a malformed newer result arrives.
            if (InventoryStatus is OrderInventoryStatus.Rejected or OrderInventoryStatus.Released
                or OrderInventoryStatus.Expired or OrderInventoryStatus.Committed) return false;
            if (InventoryStatus == OrderInventoryStatus.Reserved && result == OrderInventoryStatus.Reserved) return false;
            if (reason?.Length > 256) throw new ArgumentException("Inventory reason is too long.");

            InventoryReservationVersion = version;
            InventoryStatus = result;
            InventoryFailureReason = result == OrderInventoryStatus.Reserved ? null : reason;
            Status = result switch
            {
                OrderInventoryStatus.Reserved => OrderStatus.InventoryReserved,
                // No commit command is issued at this stage: an unsolicited commit needs investigation.
                OrderInventoryStatus.Committed => OrderStatus.RequiresAttention,
                _ => OrderStatus.Failed
            };
            return true;
        }

        public bool FlagInventoryCommandRejection(Guid requestId, string operation, string reason)
        {
            if (requestId != InventoryReservationRequestId || operation != "Reserve" ||
                InventoryReservationVersion != 0 || InventoryStatus != OrderInventoryStatus.Requested) return false;
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 200)
                throw new ArgumentException("A bounded rejection reason is required.");
            var failure = "ReserveCommandRejected: " + reason;
            if (InventoryFailureReason == failure) return false;
            Status = OrderStatus.RequiresAttention;
            InventoryFailureReason = failure;
            // This diagnostic is unversioned; a subsequent authoritative result may still resolve it.
            return true;
        }

    }
}
