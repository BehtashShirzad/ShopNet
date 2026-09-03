# Inventory runtime — stage 2 checkpoint

Inventory now has Domain, Application, Infrastructure and a gRPC API under `src`,
with unit and Docker/Testcontainers integration tests under `test`.
This checkpoint does not change Catalog, Cart or Order implementation, migrate
Catalog stock, or implement an Order saga. The root solution and shared V1 contracts
are updated only to include this Inventory stage.

## Ownership and boundaries

- Catalog owns product metadata/price. Its V1 `ProductCreated` initializes an
  Inventory item with **zero** stock. Re-delivery never resets an existing balance.
- Inventory owns physical stock, reserved stock and reservation lifecycle.
- gRPC batch availability is advisory, not a reservation or checkout guarantee.
  Unknown/inactive products report zero sellable stock. Expired reservations can
  conservatively reduce reported availability until the expiry scan releases them.
- `ReceiveInventoryStock` records an actual warehouse receipt once per `ReferenceId`.
  It is not a Catalog stock synchronization command. The product must already exist.
  Use a distinct reference ID per receipt line/command, including different products
  on the same supplier document; reusing it with a different payload is rejected.
- Multi-product reservation is one local SQL transaction, not a distributed saga.
  No product changes until every line is valid. A rejected attempt stays rejected;
  a genuinely new business attempt needs a new `ReservationRequestId`.

## Transport contracts

Shared types live in `ShopNet.Contracts.Inventory.V1`.

| Input | Transport / destination | Behavior |
| --- | --- | --- |
| Catalog V1 `ProductCreated` | Publish; durable `inventory-catalog-products-v1` subscription | Register zero-stock product |
| `ReserveInventory` | Send to `inventory-commands-v1` | Reserve all lines or reject all |
| `CommitInventory` | Send to `inventory-commands-v1` | Decrease on-hand and reserved quantities |
| `ReleaseInventory` | Send to `inventory-commands-v1` | Release reserved quantities |
| `ReceiveInventoryStock` | Send to `inventory-stock-receipts-v1` | Add a physical stock receipt once |
| `GetAvailability` | `inventory.v1.InventoryAvailabilityService` gRPC | Read 1–100 product IDs |

The command queues deliberately do not subscribe to published commands or
`OrderCreated`. Use `Send`, not `Publish`, for inventory mutations.
The optional `Inventory:QueuePrefix` is for isolated environments/tests; senders
must address the same prefixed queues. Leave it empty for the names above.

Outputs are `InventoryReserved`, `InventoryRejected`, `InventoryCommitted`,
`InventoryReleased`, `InventoryExpired`, and `InventoryCommandRejected`.
Each carries `OrderId` and `ReservationRequestId`. State outcomes additionally
carry `ReservationVersion` (1 initially, 2 on finalization) and a stable `EventId`.

Future Order handling must correlate **both** IDs, deduplicate `EventId`, and ignore
an older `ReservationVersion`. Outbox deliveries across separate transactions can
arrive out of order. `InventoryCommandRejected` is not a reservation-state transition.

Replays return the **current** outcome with the same event identity/version, never
an obsolete success that resurrects released/expired stock. Replaying a terminal
attempt through Commit/Release returns its actual terminal state; callers must not
interpret every result as successful completion of the requested operation.

Release before Reserve persists a cancellation tombstone. A later Reserve with that
attempt ID cannot reserve stock. A late command for an old attempt cannot modify a
new one. Another active or committed attempt for the same Order is prohibited.

Reservation batches contain 1–100 unique products with positive quantities.
Expiry must be in the future and no more than 24 hours away. Quantity/expiry changes
with the same request ID yield `InventoryCommandRejected` with `RequestIdConflict`.
Malformed commands fault without mutating inventory; valid business rejection
produces an explicit outcome. A worker scans at most 100 expired attempts every
5 seconds, with a fresh transaction per attempt, and retries failed work on the next
scan. Commit checks the deadline again after acquiring product locks.

## Persistence, concurrency and delivery

`InventoryDbContext` contains inventory items, required child reservations, durable
reservation attempts and stock receipts, plus the MassTransit outbox schema.
SQL balance constraints, unique indexes and rowversion columns reinforce invariants.
Domain UTC timestamps are restored with `DateTimeKind.Utc` after SQL materialization.

Each mutation creates one SQL transaction. Transaction-owned application locks are
acquired in order: request, order, then sorted products (receipts: reference, product).
Registration locks its product. Locks serialize conflicting work across processes,
while unrelated orders/products remain independent. Lock failure aborts the operation.
Do not add writers that bypass this application boundary.

Consumers create a fresh application scope, separate from MassTransit's consumer
scope. The operation uses the scoped **Bus Outbox**, saving inventory, business
deduplication records and outgoing messages together before acknowledging the input.
This separation is intentional: inheriting `ConsumeContext` publishing can bypass
the Bus Outbox. Integration tests verify paused consumer output is stored in SQL.
The consumer outbox middleware is not layered over this independently owned transaction.
The `InboxState` table is part of the MassTransit schema; business request/receipt
records, not that table, provide this implementation's permanent deduplication.

Discard the entire scope after an exception; never retry a failed tracked context.
Broker delivery retries use new application scopes. Short retries are configured;
after exhaustion, investigate/replay messages from the durable `_error` queues.
Stock received before product registration may need replay after Catalog catch-up.
Successful replay can enqueue another copy of the same outcome event. Exactly-once
network delivery is not claimed; downstream consumers must be idempotent.

Internal domain events are cleared after commit. There are no external low-stock,
stock-adjustment or replenishment handlers in this checkpoint. Outgoing integration
events are the explicit reservation outcomes, not automatic copies of every domain event.
Reservation and receipt history is retained for deduplication; any future archival
policy must preserve that guarantee. Large historical collections may need a
targeted-loading/archive design before high-volume deployment.

Design references: [MassTransit outbox behavior](https://masstransit.io/documentation/patterns/transactional-outbox)
and [SQL Server transaction-owned application locks](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql).
The implementation stays on the repository's MassTransit **8.3.6** packages.

## Run and rollout

The checked-in configuration is local development only: gRPC HTTP/2 on
`http://localhost:5084`, local SQL integrated authentication and local RabbitMQ guest.
Use environment variables for real connection strings/credentials. This is an
internal service, not an authenticated public stock-management API. Before deployment,
enforce private networking/TLS and broker permissions: only Order may publish to the
reservation command destination, and only trusted warehouse/admin publishers may
publish stock receipts. Catalog may publish product registration events.

Run these PowerShell commands when you choose to migrate your Inventory database:

```powershell
# Set ConnectionStrings__InventoryServiceConnection and RabbitMq__* for your environment first.
dotnet ef database update --project N:\Projects\Dotnet\Shop\ShopNet\InventoryService\src\InventoryService.Infrastructure --context InventoryDbContext
dotnet run --project N:\Projects\Dotnet\Shop\ShopNet\InventoryService\src\InventoryService.Api --no-launch-profile
```

Startup does **not** auto-migrate the database. Apply the complete migration chain
before starting consumers. These migrations create Inventory's own database schema;
they do not move Catalog data. Rolling back the initial migration deletes inventory,
reservation/receipt history and pending messages; do not do that to a live database.

Keep the staged rollout gates:

1. Migrate and start Inventory; confirm its durable Catalog subscription exists.
2. Only then may Catalog use `CatalogOutbox__DeliveryEnabled=true` and restart to
   deliver queued product registrations. No Catalog configuration was changed here.
3. Keep `InventoryOutbox__DeliveryEnabled=false` until Order's durable subscriptions
   for **all** inventory results are deployed. Then enable it and restart Inventory.
4. Move Order/Cart to the new contracts in their own commit checkpoints. Only after
   controlled historical product/stock backfill and cutover, remove Catalog's old Stock.

Monitor pending OutboxMessage counts and `_error` queues during rollout. A broker
being available is not sufficient to enable delivery: unbound published events can
be lost. Paused delivery intentionally accumulates messages in SQL and is a rollout
gate, not a permanent production setting. `Inventory__ExpiryEnabled=false` can pause
expiry scanning for maintenance; the default is enabled.

## Verification

Docker Desktop must be running with Linux containers. Testcontainers creates its own
SQL Server 2022 and RabbitMQ containers and isolated databases/queues. Tests apply
migrations only to those databases; no application database migration was performed.

```powershell
dotnet test N:\Projects\Dotnet\Shop\ShopNet\InventoryService\InventoryService.slnx
dotnet test N:\Projects\Dotnet\Shop\ShopNet\Building-Blocks\test\BuildingBlocks.UnitTests\BuildingBlocks.UnitTests.csproj
```

Verified on 2026-09-03:

- Inventory unit: 98 passed (68 existing, 30 new).
- Inventory integration: 23 passed on two consecutive final runs, including real SQL/RabbitMQ/gRPC, concurrent
  last-stock competition, duplicate attempts/receipts, opposite-order batch locks,
  commit vs release/expiry, SQL-flush rollback, stale rowversion writes and outbox restart.
- Shared unit: 15 passed (3 new contract tests).
- Regression unit tests: Catalog 30, Cart 23, Order 15 passed.
- EF reports no pending model changes. Inventory build has no warnings/errors.

Existing NU1903 warnings for Microsoft.OpenApi 2.4.1 remain in the other services;
that unrelated dependency upgrade is not part of this checkpoint. No commit was made
automatically. Stop here for review, test and commit before the Order stage.
