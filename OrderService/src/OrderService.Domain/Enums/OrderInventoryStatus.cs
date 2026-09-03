namespace OrderService.Domain.Enums;

// Independent from payment/shipping lifecycle; numeric values are persisted.
public enum OrderInventoryStatus
{
    Requested = 1, Reserved = 2, Rejected = 3, Released = 4, Expired = 5, Committed = 6
}
