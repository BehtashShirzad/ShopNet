# ShopNet

ShopNet is a .NET e-commerce system built as a set of independently deployable microservices. Each service owns its domain boundary and data store. Production code is organized into API, Application, Domain, and Infrastructure layers.

## Services

| Service | Responsibility | Storage | Interfaces |
| --- | --- | --- | --- |
| Catalog | Products and categories | SQL Server | HTTP, gRPC, RabbitMQ |
| Inventory | Stock levels, reservations, releases, and stock consumption | SQL Server | gRPC, RabbitMQ |
| Cart | Shopping carts and checkout | Redis | HTTP, gRPC, RabbitMQ |
| Order | Order creation and order state | SQL Server | HTTP, gRPC, RabbitMQ |
| Identity | Keycloak-facing account API | Keycloak | HTTP, OpenID Connect |
| Keycloak | External identity management for the ShopNet realm | PostgreSQL | OpenID Connect |

Shared projects are located under `Building-Blocks`:

- `Domain.Abstractions`: base aggregate, entity, and domain event types
- `Application.Abstractions`: application-layer contracts
- `Infrastructure.Abstractions`: infrastructure contracts
- `ShopNet.Contracts`: integration messages and cross-service contracts

## Architecture

Catalog owns product information and does not store stock quantities. Inventory is the sole owner of stock levels and reservations.

Cart uses gRPC to read product information and available inventory. Checkout publishes a message through RabbitMQ. Order creates the order and sends an inventory reservation command. Inventory processes the command and publishes the reservation result back to Order.

Asynchronous messaging uses MassTransit and RabbitMQ. Catalog, Inventory, and Order use the Entity Framework Core transactional outbox. Cart uses a Redis-backed checkout outbox so a checkout message is retained when delivery fails temporarily.

The complete order, payment, and reservation workflow is not yet implemented as a saga.

## Technology

- .NET and ASP.NET Core
- Entity Framework Core
- SQL Server
- Redis
- RabbitMQ and MassTransit
- gRPC
- Keycloak and OpenID Connect
- Docker Compose and Docker Buildx Bake
- xUnit and Testcontainers

## Prerequisites

- Docker Desktop with Docker Compose
- A .NET SDK compatible with the projects' target framework
- Git

Running the complete system through Docker does not require local installations of SQL Server, Redis, RabbitMQ, or PostgreSQL.

## Environment configuration

Create a local environment file from the provided example:

```powershell
Copy-Item .env.example .env
```

The default values are intended only for local development. Change all passwords and client secrets before using a shared or production environment. Do not commit the `.env` file.

## Run the complete system

Run the following command from the repository root:

```powershell
docker compose up -d --build
```

This builds the ShopNet application images and starts all application and infrastructure containers. Database migrations are applied when the services start.

Check container status:

```powershell
docker compose ps
```

Follow all logs:

```powershell
docker compose logs -f
```

Follow one service:

```powershell
docker compose logs -f inventory
```

Stop the environment while retaining its data:

```powershell
docker compose down
```

Remove the environment and its development volumes:

```powershell
docker compose down -v
```

The last command permanently removes the development databases and the data stored by Redis and RabbitMQ.

## Build application images

Build the Cart, Catalog, Identity, Inventory, and Order images together:

```powershell
docker buildx bake
```

This command builds only the application images. SQL Server, Redis, RabbitMQ, PostgreSQL, and Keycloak use the upstream images declared in `compose.yaml`.

## Default ports

| Component | Host address |
| --- | --- |
| Identity API | `http://localhost:5239` |
| Catalog HTTP | `http://localhost:6002` |
| Catalog gRPC | `http://localhost:60002` |
| Inventory gRPC | `http://localhost:5084` |
| Order HTTP | `http://localhost:6001` |
| Order gRPC | `http://localhost:60001` |
| Cart HTTP | `http://localhost:6003` |
| Keycloak | `http://localhost:8080` |
| RabbitMQ Management | `http://localhost:15672` |
| SQL Server | `localhost:1433` |
| Redis | `localhost:6379` |
| RabbitMQ AMQP | `localhost:5672` |

Every host port can be overridden through the variables documented in `.env.example`.

## Keycloak

Docker Compose runs Keycloak with a dedicated PostgreSQL database. The ShopNet realm is imported from the following file during the first startup:

```text
deploy/keycloak/shopnet-realm.json
```

Open the administration console at:

```text
http://localhost:8080/admin/master/console/
```

Default local administrator credentials:

```text
Username: admin
Password: ShopNet!KeycloakAdmin2026
```

After signing in, switch from the `master` realm to the `shopnet` realm.

The ShopNet account console is available at:

```text
http://localhost:8080/realms/shopnet/account/
```

To allow users to create their own accounts, open the `shopnet` realm, go to `Realm settings > Login`, and enable `User registration`.

The `IdentityService/src/Keycloak.Client` project supports registration, login, token refresh, logout, user lookup, password reset, and user deletion. IdentityService exposes these operations through an application-level identity provider abstraction. Keycloak is the source of truth for users and tokens; IdentityService no longer issues tokens through OpenIddict or stores a second local user account.

## Run services outside Docker

Start the required infrastructure first:

```powershell
docker compose up -d sqlserver redis rabbitmq keycloak-db keycloak
```

Restore and build the solution:

```powershell
dotnet restore shopnet.slnx
dotnet build shopnet.slnx
```

Run an API project directly. For example:

```powershell
dotnet run --project CatalogService/src/CatalogService.Api/CatalogService.Api.csproj
```

When a service runs outside Docker, its development settings and dependency addresses must use the host ports instead of Docker network names.

## Tests

Production projects are stored under each service's `src` directory. Test projects are stored in the adjacent `test` directory.

Run the complete test suite:

```powershell
dotnet test shopnet.slnx
```

Run the Inventory test projects:

```powershell
dotnet test InventoryService/test/InventoryService.UnitTest/InventoryService.UnitTest.csproj
dotnet test InventoryService/test/InventoryService.IntegrationTests/InventoryService.IntegrationTests.csproj
```

Integration tests use Testcontainers to start real dependencies such as SQL Server, Redis, and RabbitMQ. Docker Desktop must be running. Testcontainers selects available host ports and removes the test containers after the run.

Run the Keycloak client tests:

```powershell
dotnet test IdentityService/test/Keycloak.Client.UnitTests/Keycloak.Client.UnitTests.csproj
```

## Repository layout

```text
ShopNet/
|-- Building-Blocks/
|   |-- src/
|   `-- test/
|-- CartService/
|   |-- src/
|   `-- test/
|-- CatalogService/
|   |-- src/
|   `-- test/
|-- IdentityService/
|   |-- src/
|   `-- test/
|-- InventoryService/
|   |-- src/
|   `-- test/
|-- OrderService/
|   |-- src/
|   `-- test/
|-- deploy/keycloak/
|-- compose.yaml
|-- docker-bake.hcl
|-- .env.example
`-- shopnet.slnx
```

## Development guidelines

- Include unit tests with service changes. Add integration tests when a change involves a database, cache, message broker, or service boundary.
- Keep cross-service messages in `ShopNet.Contracts`. Do not add direct references between service domain projects.
- Do not add stock fields to Catalog. Inventory owns all stock rules and state.
- Persist database changes and outgoing messages through the service's outbox.
- Do not commit real secrets, certificates, passwords, or local `.env` files.
