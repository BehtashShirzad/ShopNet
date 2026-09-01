namespace InventoryService.Domain.Enums;

public enum ReservationReleaseReason
{
    None = 0,
    OrderCancelled = 1,
    PaymentFailed = 2,
    InventoryCompensation = 3,
    AdministratorAction = 4,
    Expired = 5
}