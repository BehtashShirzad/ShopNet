namespace InventoryService.Domain.Enums;

public enum StockAdjustmentReason
{
    None = 0,
    StockCountCorrection = 1,
    Damaged = 2,
    Lost = 3,
    CustomerReturn = 4,
    SupplierReturn = 5,
    AdministratorCorrection = 6
}