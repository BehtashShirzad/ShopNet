namespace ShopNet.Contracts.IntegrationEvents.Catalog.V1;

/// <summary>
/// A product was registered in Catalog. Inventory may initialize its own zero-stock
/// record; this message does not grant stock or reserve it.
/// </summary>
public sealed record ProductCreated(Guid ProductId) : IntegrationEvent;
