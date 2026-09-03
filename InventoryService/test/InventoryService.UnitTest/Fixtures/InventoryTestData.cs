using InventoryService.Domain.Aggregates;

namespace InventoryService.UnitTest;

internal static class InventoryTestData
{
    internal static readonly DateTime ReservedAtUtc =
        new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    internal static readonly DateTime ExpiresAtUtc =
        ReservedAtUtc.AddHours(1);

    internal static InventoryItem CreateInventory(
        int initialQuantity = 10,
        int reorderPoint = 2)
    {
        var inventory = InventoryItem.Create(
            Guid.NewGuid(),
            initialQuantity,
            reorderPoint);

        inventory.ClearEvents();
        return inventory;
    }
}
