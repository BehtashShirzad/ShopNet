# Order / Inventory integration — stage 3 checkpoint

This stage changes **Order only**. Catalog, Cart, Inventory production code and the
shared contracts are unchanged. The integration-test project references Inventory
Infrastructure to compose the real services in tests; production Order projects do
not reference any Inventory implementation assembly.

## What runs now

1. The existing `CartCheckedOutEvent` reaches the existing durable
   `cart-checked-out-event-handler` queue.
2. Order validates the checkout snapshot, creates one Pending order, freezes its
   lines and persists a new `InventoryReservationRequestId` with a fixed deadline.
3. The same SQL transaction persists `ReserveInventory` to the Order Bus Outbox,
   addressed to `inventory-commands-v1`. The existing `OrderCreatedEvent` remains a
   separate informational event for compatibility; Inventory does not reserve from it.
4. Inventory replies through the existing V1 outcome contracts. Order consumes all
   six result types on durable `order-inventory-results-v1`.
5. The order's GET response includes reservation ID, expiry, version, inventory status
   and failure reason in addition to the existing fields.

There is **no full saga, payment initiation, inventory Commit/Release sender,
shipping, automatic new reservation attempt, or public cancellation API** in this
checkpoint. A successful reservation stops at InventoryReserved. Inventory's expiry
worker releases it if nothing finalizes it; Order then becomes Failed. This is an
intermediate integration checkpoint, not a completed paid-checkout flow.

## State behavior

| Result for the current attempt | Order status | Inventory status |
| --- | --- | --- |
| No result yet | Pending | Requested |
| InventoryReserved | InventoryReserved | Reserved |
| InventoryRejected | Failed | Rejected |
| InventoryReleased | Failed | Released |
| InventoryExpired | Failed | Expired |
| InventoryCommitted (unsolicited at this stage) | RequiresAttention | Committed |
| Reserve command rejected before a versioned result | RequiresAttention | Requested |

`RequiresAttention = 8` is appended to OrderStatus; existing numeric values do not
change. An unsolicited commit must not confirm an order or initiate payment. An
unversioned command rejection is diagnostic: a subsequent authoritative inventory
result can still resolve it. Rejections for Commit/Release are ignored because Order
does not issue those operations yet.

Results are correlated by **OrderId + ReservationRequestId**. Versioned duplicates
and older versions do nothing. Terminal inventory outcomes cannot be resurrected,
including when Expired v2 arrives before Reserved v1. A Reserved result must match
the exact product quantities and persisted deadline. Another attempt's result cannot
change this order. Unknown order IDs and malformed results go through message
failure handling for investigation/replay rather than silently being acknowledged.

The retained reservation version is the idempotency boundary for outcomes, including
replays with a different transport MessageId. It is not a generic inbox audit of every
received event. A future explicit retry/saga must create and persist a new request ID,
reset its current-attempt version safely, and continue fencing old-attempt results.
No such retry operation is exposed now.

## Checkout validation and deduplication

- Exactly 1–100 distinct, non-empty ProductIds, nonblank names, positive quantities
  and prices fitting decimal(18,2). Total must equal the sum of line prices × quantities.
- Prices remain the trusted Cart checkout snapshot. Order does not query Catalog or
  reprice it in this stage; Cart's quote/availability redesign comes next.
- An exact checkout replay is a no-op. It does not create another reservation or
  extend its deadline, including after a terminal outcome.
- Reusing CartId with a different customer or different line content is rejected.
- Transaction-owned SQL application locks serialize same-Cart creation and same-Order
  result handling across processes. A filtered unique CartId index and rowversion
  provide additional database protection. Do not bypass the command pipeline in new writers.

## Transactions and delivery

The MediatR command transaction now includes the order and both outgoing messages.
Domain events dispatch **before** the EF save, so handlers enqueue through the scoped
Bus Outbox. IApplicationDbContext and IUnitOfWork resolve the exact same scoped
WriteDbContext as repositories and the outbox. Both async SaveChanges overloads and
IUnitOfWork use this path; synchronous saves are blocked.

Each RabbitMQ delivery creates a fresh application scope without inheriting the
consumer Publish/Send endpoint. Order's pipeline owns the transaction; the Bus Outbox
owns durable outgoing storage. Do not add a second consumer-outbox transaction over
this boundary. InboxState is included as part of the MassTransit schema, while business
CartId/request-version checks provide this implementation's deduplication.
Dispose the scope after any failed command or outer rollback; do not retry a failed
tracked context. Short broker retries are configured, with ArgumentException excluded;
after retry exhaustion inspect/replay the durable `_error` queues.

Reserve uses `Send`, with MessageId = ReservationRequestId and CorrelationId = OrderId.
The persisted command and deadline survive process restart. Transport is at-least-once;
exactly-once network delivery is not claimed. The integration tests run SQL Server,
RabbitMQ, real Order consumers/outbox, and real Inventory consumers/outbox together.

## Migration and rollout

The new migration is additive: reservation columns, rowversion, outbox tables and the
CartId uniqueness constraint. Existing orders keep null inventory state and are **not**
automatically reserved or re-emitted. Historical empty CartIds are excluded from the
unique index because the older CartId migration used an empty-Guid default.

Before applying it to an existing database, inspect duplicate non-empty CartIds:

```sql
SELECT CartId, COUNT(*) AS OrderCount
FROM Orders
WHERE CartId <> '00000000-0000-0000-0000-000000000000'
GROUP BY CartId
HAVING COUNT(*) > 1;
```

Resolve historical duplicates deliberately before migrating. Migration fails on those
duplicates; it does not delete or merge orders. Integration tests verify legacy rows
survive upgrade and duplicate data blocks upgrade without being deleted.

Apply only when you choose to migrate your application database:

```powershell
# Configure ConnectionStrings__OrderServiceConnection and RabbitMq__* first.
dotnet ef database update --project N:\Projects\Dotnet\Shop\ShopNet\OrderService\src\OrderService.Infrastructure --context WriteDbContext
dotnet run --project N:\Projects\Dotnet\Shop\ShopNet\OrderService\src\OrderService.Api --no-launch-profile
```

There is no startup auto-migration. No application database migration was executed
during implementation. Migration Down drops pending messages and reservation tracking;
do not roll it back on a live system without a recovery plan.

Default configuration keeps **OrderOutbox:DeliveryEnabled = false**. Starting Order
can create Pending orders and queue commands in SQL, but sends nothing until enabled.
Use the coordinated rollout sequence:

1. Apply Order/Inventory migrations and start both services with delivery paused.
   Confirm Inventory's command queue and Order's result queue exist with their bindings.
2. Ensure product registration has caught up and Inventory has authoritative stock.
   ProductCreated alone initializes zero stock; this stage does not backfill Catalog stock.
3. Enable `InventoryOutbox__DeliveryEnabled=true` only after Order's result subscription
   exists, and restart Inventory to release any result backlog.
4. Enable `OrderOutbox__DeliveryEnabled=true` only after the Inventory command queue
   is ready, and restart Order. Complete the Cart/backfill cutover before real traffic.

`Inventory__ReservationMinutes` defaults to 15 and accepts integer minutes from 1
through 1440. `Inventory__CommandQueue` defaults to `inventory-commands-v1`; an isolated
environment's prefix must match Inventory's actual queue. `Order__QueuePrefix` prefixes
Order's two receive queues. Existing deadlines never get extended during replay.
Commands delayed in a paused outbox beyond their deadline will be rejected by Inventory;
do not reinterpret them as new reservations.

Monitor aged Pending orders, RequiresAttention, OutboxMessage backlog and `_error`
queues. Order-side saga timeouts/reconciliation are deferred; a failed command in an
error queue is not automatically converted into a final Order state. Broker connectivity
alone does not guarantee that downstream subscriptions and stock data are ready.

No authentication or public API hardening was performed. The existing user-id fallback
in ContextHelper remains unchanged; this checkpoint is not authorization to expose
that development API publicly. Broker credentials/ACLs must restrict Cart checkout
publication to Cart and inventory outcome publication to Inventory.

## Tests / commit checkpoint

With Docker Desktop running Linux containers:

```powershell
dotnet test N:\Projects\Dotnet\Shop\ShopNet\OrderService\OrderService.slnx
```

Verified on 2026-09-03:

- 52 Order unit tests passed (15 existing, 37 new).
- 18 Order integration tests passed (3 existing, 15 new).
- Real cross-service success, multi-line rejection and expiry; paused consumer output
  followed by restart delivery; SQL-flush rollback and actual SQL insert failure;
  concurrent duplicate checkout/results; stale rowversion; legacy/duplicate-data migration.
- Regression unit tests using unchanged compiled projects: Inventory 98, Cart 23,
  Catalog 30, BuildingBlocks 15 passed.
- EF reports no pending model changes.

Known pre-existing build warnings remain: NU1903 for Microsoft.OpenApi 2.4.1 and the
old lowercase migration class name. No dependency upgrade outside this stage was made.
No commit was created automatically. Stop here for review/test/commit before Cart.
