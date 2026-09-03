variable "TAG" {
  default = "local"
}

group "default" {
  targets = ["cart", "catalog", "identity", "inventory", "order"]
}

target "cart" {
  context    = "."
  dockerfile = "CartService/src/CartService.Api/Dockerfile"
  tags       = ["shopnet/cart-service:${TAG}"]
}

target "catalog" {
  context    = "."
  dockerfile = "CatalogService/src/CatalogService.Api/Dockerfile"
  tags       = ["shopnet/catalog-service:${TAG}"]
}

target "identity" {
  context    = "."
  dockerfile = "IdentityService/src/IdentityService/Dockerfile"
  tags       = ["shopnet/identity-service:${TAG}"]
}

target "inventory" {
  context    = "."
  dockerfile = "InventoryService/src/InventoryService.Api/Dockerfile"
  tags       = ["shopnet/inventory-service:${TAG}"]
}

target "order" {
  context    = "."
  dockerfile = "OrderService/src/OrderService.Api/Dockerfile"
  tags       = ["shopnet/order-service:${TAG}"]
}
