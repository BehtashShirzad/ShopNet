namespace ShopNet.Authorization;

public static class CatalogPermissions
{
    public const string ProductCreate = "Catalog.Product.Create";
    public const string ProductUpdate = "Catalog.Product.Update";
    public const string CategoryCreate = "Catalog.Category.Create";
    public const string CategoryUpdate = "Catalog.Category.Update";
    public const string InternalRead = "Catalog.Internal.Read";
    public static readonly string[] All =
        [ProductCreate, ProductUpdate, CategoryCreate, CategoryUpdate, InternalRead];
}

public static class CartPermissions
{
    public const string Read = "Cart.Read";
    public const string Write = "Cart.Write";
    public const string Checkout = "Cart.Checkout";
    public static readonly string[] All = [Read, Write, Checkout];
}

public static class InventoryPermissions
{
    public const string InternalRead = "Inventory.Internal.Read";
    public static readonly string[] All = [InternalRead];
}

public static class OrderPermissions
{
    public const string ReadOwn = "Order.ReadOwn";
    public const string ReadAny = "Order.ReadAny";
    public static readonly string[] All = [ReadOwn, ReadAny];
}
