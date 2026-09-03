# Cart / Inventory checkout — stage 4 checkpoint

This checkpoint changes **Cart only**. Its integration-test project references
Order Infrastructure to run real Order consumers/SQL; production Cart has no
reference to Order's implementation. Catalog, Inventory and Order source and
shared message contracts are unchanged. No application database was migrated,
no existing stock was backfilled, and no commit was created automatically.

## Ownership and transport

- Catalog supplies product identity, name and price through gRPC. Cart no longer
  reads Catalog Stock. Field 5 is reserved in Cart's local protobuf copy, so old
  Catalog servers remain wire-compatible. Catalog's actual Stock column/API still
  exists until the separately reviewed backfill/cutover stage.
- Inventory supplies a batch availability read through gRPC at checkout. Unknown,
  inactive or insufficient products reject checkout. Incomplete, contradictory or
  duplicate responses fail closed. This is advisory: **it does not reserve stock**.
- Checkout sends the existing `CartCheckedOutEvent` asynchronously through RabbitMQ.
  Order owns the subsequent reservation request and handles Inventory outcomes.
  A successful HTTP checkout means the snapshot is accepted for delivery, not that
  stock is reserved or payment/order fulfillment has succeeded.
- No saga, payment flow, reservation Commit/Release, or new reservation attempt is
  implemented here. See the existing Order checkpoint for current limitations.

## Cart and HTTP behavior

The cart is limited to 100 distinct products, positive quantities and positive
prices fitting decimal(18,2), matching Order's input contract. Increasing a quantity
cannot wrap around an integer. A checked-out cart cannot be edited. Its original
`CheckoutEventId` and UTC checkout timestamp are retained.

Catalog prices are checked again at checkout. A changed price returns
`409 price_changed`, without changing the stored cart or emitting an event. The
client must create/review a new cart at the current prices. There is intentionally
no silent repricing and no new quote-refresh endpoint/UI in this checkpoint.

New checkout failures use ProblemDetails:

| HTTP | Title | Action |
| --- | --- | --- |
| 404 | cart_not_found / product_not_found | Check identity/ownership or product availability. |
| 409 | empty_cart / insufficient_stock / price_changed / cart_closed | Review the cart; no new checkout was accepted. |
| 409 | cart_changed | Another request changed the cart. Reload, then retry the same CartId. |
| 503 | dependency_unavailable | Catalog/Inventory could not be verified; no checkout was persisted. |
| 503 | cart_storage_unavailable | Storage completion may be ambiguous. Reload/retry the same CartId. |

The checkout route still requires authorization and verifies cart ownership.
Successful retries of an already checked-out cart return the same CartId before
calling remote services, and never enqueue another event. GET includes
`isCheckedOut` and `checkoutEventId`. Existing HTTP success shape (200 + CartId) is
preserved. Other pre-existing API error handling and development user-id fallback
have not been redesigned. The tests use a test-only authentication handler, not
the deployed identity provider; do not treat this as public API/auth hardening.

## Redis concurrency and outbox

All repository writers use an exact loaded-JSON compare-and-set. A stale edit
cannot overwrite newer content, revive an expired cart, or undo checkout. The
repository and checkout-store interfaces share the same scoped instance. Dispose
the request scope after an error/ambiguous timeout; reload in a fresh scope.

One Lua operation verifies the expected snapshot and key types, then saves the
immutable cart, event payload and delivery index together. Ordinary StoreCart
cannot persist a checked-out cart and bypass the outbox. Redis scripts provide
atomic visibility, but are not general rollback transactions: key-type checks must
precede all writes. Do not add fallible commands after writes without reviewing
their failure behavior.

Default keys preserve the old raw `cartId.ToString()` cart key. Optional
`Redis:KeyPrefix` isolates environments; changing it does not migrate existing carts.
Outbox keys under that prefix are `cart:checkout:messages`, `cart:checkout:pending`
and `cart:checkout:leases`. The current multi-key layout supports standalone Redis
or Sentinel, **not Redis Cluster**: these keys do not share a cluster hash slot.

Active carts retain their 24-hour TTL. New checked-out carts and pending messages
have no TTL. Successful publication removes the payload/index/lease, not the
checked-out cart. Retaining its identity prevents replay after normal cart expiry.
A future archive/retention design must preserve deduplication; storage growth must
be monitored. The outbox is not a permanent message audit log.

The delivery worker scans up to 20 messages per pass, every second. Redis server
time controls 60-second leases and 5-second retry delays. Publishing has a 10-second
timeout. ACKs are token-fenced: an expired owner cannot erase another worker's
delivery. A crash after RabbitMQ confirmation but before Redis ACK can republish
the **same EventId**. MessageId = EventId, CorrelationId = CartId. Delivery is
at-least-once; Order's persistent CartId/content check makes the replay a no-op.

Redis now holds pending business messages, not just disposable cache data. Before
real traffic, provision persistent storage/AOF, backups, suitable capacity and a
`noeviction` policy, and restrict key access to Cart. AOF/fsync and failover settings
define the possible data-loss window; scripts alone do not guarantee survival of
power loss, lost disks or asynchronous-replica failover. No Redis server settings
or developer data were changed by this checkpoint. If those durability limitations
are unacceptable, move cart and outbox together to a transactional database in a
separate persistence redesign; do not dual-write independent stores.

Older JSON carts remain readable. An old `IsCheckedOut=true` cart without an event
identity is not automatically republished: the old implementation's delivery status
cannot safely be inferred. Reconcile historical missing orders deliberately. Old
checked-out carts retain their old TTL; the new permanent marker applies only to
checkouts accepted by this implementation.

## Configuration and coordinated cutover

`CartOutbox:DeliveryEnabled` defaults to **false**. Checkout still persists pending
work while this is false, but it will not create an Order until delivery is enabled.
Treat this as a rollout gate, not the final production setting. The setting is read
at startup; restart Cart after changing it.

1. Complete the existing Order/Inventory migrations, durable queue deployment and
   authoritative product/stock backfill. ProductCreated only registers zero stock.
2. Follow Order's rollout instructions to enable Inventory results and Order commands
   only after the corresponding durable destinations/bindings exist. Confirm Order's
   `cart-checked-out-event-handler` subscription is ready before Cart delivery.
3. Configure Cart's Redis durability and service/broker endpoints. With old Cart
   instances stopped, deploy the new Cart writer: legacy instances do not honor CAS
   or immutable checkouts, so mixed-version writers are not safe during cutover.
4. Set `CartOutbox__DeliveryEnabled=true`, restart Cart, and observe backlog delivery,
   Order creation and reservation outcomes before admitting real checkout traffic.
5. Only after an explicitly reviewed stock backfill/cutover may Catalog's legacy Stock
   be removed. That is the next checkpoint, not part of this change.

Local defaults are `Grpc__CatalogService=http://localhost:60002` and
`Grpc__InventoryService=http://localhost:5084` (HTTP/2). `Grpc__TimeoutSeconds` defaults
to 5, accepts 1–30, and applies to each RPC; Catalog calls are sequential per line.
Use actual private/TLS endpoints in deployment. Redis accepts `Redis__ConnectionString`
or the legacy `Redis__Configuration`; RabbitMQ uses the existing `RabbitMq__*` keys.
Production must also provide its existing identity configuration. The formerly empty
Production JSON file is now a valid empty object; the Catalog fallback port was fixed.

Broker connectivity/publisher confirmation alone does not prove a consumer binding
exists. Unbound published events can be discarded. Monitor pending count/age,
delivery errors, Redis capacity/persistence health, Order Pending/RequiresAttention,
and RabbitMQ error queues. A message acknowledged by RabbitMQ can subsequently fail
inside Order; Cart does not requeue such downstream business failures automatically.

## Tests and commit checkpoint

With Docker Desktop running Linux containers:

```powershell
dotnet test N:\Projects\Dotnet\Shop\ShopNet\CartService\CartService.slnx
```

Verified on 2026-09-03: **57 unit tests and 38 integration tests passed** (34 new
unit cases and 33 new integration cases). Testcontainers starts isolated Redis 7,
RabbitMQ 3.13 and SQL Server 2022 instances. Databases and Redis key namespaces are
test-only. gRPC clients use real protobuf/HTTP2 against controlled TestServer upstreams;
these are not full deployed Catalog/Inventory instances. Actual Order production
consumers, SQL transactions and outbox are used for Cart-to-Order messaging tests.

Coverage includes immutable snapshots, validation/price mismatch, malformed/unavailable
gRPC responses, deadlines/cancellation, ownership, concurrent checkout, stale edits,
Redis script preflight failure, expired keys/leases, failed publishing, process restart,
publish-before-ACK replay, real Order deduplication, and legacy JSON compatibility.

The pre-existing NU1903 warning for Microsoft.OpenApi 2.4.1 remains; a clean rebuild
of the referenced Order infrastructure can also show its old lowercase migration
class warning. No unrelated package upgrade was made. Stop here for review, your
test run and commit before the Catalog cleanup stage.

Design references: [Redis scripting](https://redis.io/docs/latest/develop/programmability/eval-intro/),
[rollback limitations](https://redis.io/blog/you-dont-need-transaction-rollbacks-in-redis/),
[persistence](https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/),
and [eviction](https://redis.io/docs/latest/develop/reference/eviction/).
