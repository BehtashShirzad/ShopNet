# Catalog → Inventory: stage 1

This stage prepares Catalog only. Cart, Order and Inventory implementations are
unchanged. Catalog still accepts and returns legacy `Stock`, including protobuf
field 5. No stock data is dropped or copied by this migration.

## New contract

`ShopNet.Contracts.IntegrationEvents.Catalog.V1.ProductCreated` contains:

- `ProductId`: Catalog product identity.
- `EventId`: stable identity of the originating domain event, distinct from ProductId.
- `OccurredOnUtc`: original event timestamp.

The legacy unversioned event is retained; new creation publishes only V1.
This event registers a product, not a stock receipt. A future Inventory consumer
must initialize a zero-stock record, deduplicate by EventId/ProductId, and must
never interpret this event as a reservation or add stock on duplicate delivery.
The eventual Inventory workflow will use explicit inventory commands.

## Atomicity and delivery

Catalog dispatches domain events before its asynchronous EF save. The handler
enqueues V1 via the scoped IPublishEndpoint, intercepted by MassTransit's EF Bus
Outbox. Product and OutboxMessage are saved in the same database transaction.
The delivery worker publishes committed rows separately and retries delivery.
Consumers must still be idempotent: delivery is at least once, not exactly once.

Use SaveChangesAsync, including its bool overload. Synchronous SaveChanges is
rejected to prevent bypassing event dispatch. Dispose the request/DbContext scope
after a failed transaction; do not reuse its tracked objects as a retry mechanism.

### Important: delivery is paused by default

In this stage Inventory has no deployed consumer yet. An exchange with no bound
queue can accept a publish without retaining it for a future subscriber.
Therefore enqueueing is always enabled, but delivery is disabled unless:

```text
CatalogOutbox__DeliveryEnabled=true
```

First deploy and bind Inventory's durable V1 subscription, then enable delivery
and restart Catalog. Previously queued messages are drained from SQL.
Do not enable delivery just because RabbitMQ is reachable. Monitor pending row
count and oldest SentTime while delivery is paused; the backlog is intentional
but grows with newly created products.

Outbox is not an event archive. Products created before this stage are not
replayed. Existing stock needs a separate controlled, idempotent backfill/cutover
after Inventory is implemented; keep the Catalog Stock column until that is done.

## Migration

Run from the repository root after reviewing the database connection:

```powershell
dotnet ef database update --project CatalogService/src/CatalogService.Infrastructure --startup-project CatalogService/src/CatalogService.Api --context WriteDbContext
```

The new migration is `20260903084859_AddCatalogTransactionalOutbox`.
It adds InboxState, OutboxState, OutboxMessage and their indexes only.
It does not change Products/Categories or start Inventory.
Apply it before starting the updated API. API startup does not auto-migrate.
Do not downgrade this migration while pending outbox messages exist: downgrade
drops the outbox tables and their pending messages.

## Tests for this checkpoint

Run from the repository root:

```powershell
dotnet test CatalogService/test/CatalogService.UnitTests/CatalogService.UnitTests.csproj
dotnet test Building-Blocks/test/BuildingBlocks.UnitTests/BuildingBlocks.UnitTests.csproj
dotnet test CatalogService/test/CatalogService.IntegrationTests/CatalogService.IntegrationTests.csproj
```

The integration suite requires Docker Desktop with Linux containers.
Testcontainers starts isolated SQL Server 2022 and RabbitMQ instances on dynamic
ports. Production connection strings are not used. Outbox tests use a separate
database per test; test containers are disposed afterwards.

New scenarios cover:

- Real HTTP product creation through MediatR, SQL and RabbitMQ V1 consumption.
- The existing gRPC product response, including legacy Stock.
- Paused delivery and replay of the original event after host restart.
- Product/outbox persistence without starting a broker connection.
- Rollback and SQL write failure leaving neither product nor message behind.
- Repeated saves/updates and validation rejection without extra Created events.
- Upgrading the old schema while preserving product stock and price.

Unit tests cover contract serialization, domain-event identity, handler mapping,
failure propagation/cancellation, scoped publishing, outbox model configuration
and Catalog's removal of its direct Cart Infrastructure reference.

This checkpoint deliberately does not implement Inventory, Cart checkout changes,
Order reservation consumers, stock backfill, or an Order saga.

## Verified checkpoint (2026-09-03)

- Catalog unit tests: 30 passed.
- Shared Building-Blocks unit tests: 12 passed.
- Catalog integration tests: 11 passed, including a second successful run.
- Existing Cart unit tests: 23 passed; existing Order unit tests: 15 passed.
- EF model/migration consistency check: no pending changes.

The real-container tests caught and now cover a previous DI bug: resolving
IApplicationDbContext used to construct a different WriteDbContext from the one
used by the outbox. The interface now aliases the same scoped instance.

Restore still reports the existing NU1903 advisory for Microsoft.OpenApi 2.4.1.
That dependency was not upgraded as part of this Inventory-preparation checkpoint.
No migration was applied to the developer's application database, and no commit
was created.
