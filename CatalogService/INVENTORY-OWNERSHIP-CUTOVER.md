# Catalog / Inventory ownership cutover — final Catalog checkpoint

Catalog no longer owns stock. Product identity, category, description, name and
price remain in Catalog; on-hand, reserved and available quantities belong only to
Inventory. This checkpoint changes Catalog source/tests only. Inventory, Cart,
Order and shared contracts are unchanged.

The project has no production products or legacy stock to transfer, as confirmed
for this cutover. Therefore no backfill command/tool was added. Creating a Catalog
product continues to publish V1 `ProductCreated`, which lets Inventory register the
same ProductId with zero stock. Physical stock must then enter Inventory through
its explicit `ReceiveInventoryStock` workflow; product creation is never a receipt.

## Removed surface

- `ProductAggregate.Stock` and the stock argument to `ProductAggregate.Create`.
- `Stock` from `CreateProductCommand`, Catalog query DTOs and HTTP request models.
- `Stock` from Catalog's gRPC response implementation.
- The `Products.Stock` SQL column through migration
  `20260903173628_RemoveLegacyCatalogStock`.
- Product price mapping is explicitly fixed to decimal(18,2) in both write and
  query contexts, matching the Cart/Order checkout contract.

Protobuf field number 5 and the name `Stock` are permanently `reserved`. They must
not be assigned to another field. This preserves safe wire evolution: an older
client can decode the new response, but observes its old Stock property as the
protobuf default zero. That value is not authoritative and must not be used. The
updated Cart client already has field 5 reserved and reads Inventory availability.

Old HTTP clients may still serialize an extra `stock` JSON property; ASP.NET's
default binder can ignore it, but Catalog will not persist or forward it. Update
clients and API documentation so callers do not mistake product creation for a
stock receipt.

## Migration safety

The migration checks for any non-zero legacy Stock before dropping the column. If
one row has `Stock <> 0`, SQL throws and the migration is not recorded or applied:

```sql
SELECT Id, Name, Stock
FROM Products
WHERE Stock <> 0;
```

For the stated empty/zero project this query returns no rows and the column is
dropped. The guard remains to protect an accidentally targeted environment. If it
ever fails, stop and reconcile those quantities into Inventory; do not edit the
migration merely to bypass the check.

`Down` can recreate the column only with zero defaults. It cannot reconstruct old
values. Rolling back the schema also does not restore Stock to the current Domain,
API or protobuf contract. Treat downgrade as emergency schema mechanics, not a
business rollback.

No developer/application database migration was run during implementation. Apply
it only after verifying the target connection:

```powershell
dotnet ef database update `
  --project N:\Projects\Dotnet\Shop\ShopNet\CatalogService\src\CatalogService.Infrastructure `
  --startup-project N:\Projects\Dotnet\Shop\ShopNet\CatalogService\src\CatalogService.Api `
  --context WriteDbContext
```

Suggested zero-data rollout:

1. Ensure Inventory is migrated/running and its durable ProductCreated subscription
   exists. Confirm Catalog's outbox rollout setting is correct for that environment.
2. Stop old Catalog writers; mixed versions could still accept a Stock value that
   the new service intentionally ignores.
3. Run the non-zero query, apply the migration, then deploy the new Catalog version.
4. Deploy/restart clients generated from the new API/protobuf contract. Verify a new
   product appears in Inventory with zero quantity, then receive stock explicitly.
5. Keep monitoring Catalog outbox messages and Inventory's Catalog consumer error
   queue. Broker reachability alone does not prove that its durable binding exists.

## Verification / commit checkpoint

With Docker Desktop running Linux containers:

```powershell
dotnet test N:\Projects\Dotnet\Shop\ShopNet\CatalogService\CatalogService.slnx
dotnet build N:\Projects\Dotnet\Shop\ShopNet\shopnet.slnx --no-restore
```

Verified on 2026-09-03:

- Catalog unit tests: 37 passed (7 new ownership/contract/configuration cases).
- Catalog integration tests: 12 passed (1 net-new case plus rewritten stock/migration
  coverage), using SQL Server and RabbitMQ Testcontainers.
- Full ShopNet solution build: succeeded with 0 errors.
- A zero-stock legacy schema upgrades while retaining product metadata and without
  fabricating ProductCreated events; a non-zero row blocks migration and remains intact.
- Real HTTP, SQL outbox, RabbitMQ V1 publication and gRPC wire response remain tested.
- EF Core reports no pending model changes after migration.

Existing warnings remain: NU1903 for Microsoft.OpenApi 2.4.1, older lowercase EF
migration class names, and existing nullable RabbitMQ configuration warnings. No
unrelated dependency/configuration cleanup was included. No commit was created.
Stop here for review, your test run and commit before any new saga/payment stage.

`CatalogOutbox:DeliveryEnabled` is explicitly `false` in the checked-in base
configuration and may be overridden with `CatalogOutbox__DeliveryEnabled=true`
after Inventory's durable subscription is ready. The Production settings file is
a valid empty JSON object and inherits that safe base default.
