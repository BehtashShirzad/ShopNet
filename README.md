# ShopNet

ShopNet is a C# microservices sample implementing a modular e‑commerce platform with separated services for catalog, cart, and orders plus shared building blocks (contracts and abstractions). It's aimed at developers learning how to structure domain, application, and infrastructure concerns across multiple services and reuse common building-block libraries.

## Stack
- Language(s): C# 
- Framework / runtime: .NET 
- Notable libraries (examples you will find across services):
  - Entity Framework Core for persistence and migrations
  - A logging framework (e.g., Serilog) for structured logging
  - API toolkits for building Web APIs and exposing OpenAPI/Swagger
  - Internal contracts and abstractions shared via `Building-Blocks/src/ShopNet.Contracts`


How it fits together:
- Each service is split into Api, Application, Domain, and Infrastructure projects following a clean architecture style. Building-Blocks contains shared contracts and abstraction libraries used by services to keep cross-service coupling low (e.g., DTOs/events, interfaces for repositories and messaging). Services communicate by using shared contracts and can be run/managed independently.

## Features
- Microservice decomposition: Catalog, Cart, Order
- Shared building-blocks for contracts and abstractions to encourage reuse
- Placeholders and helpers for EF Core migrations (see Migration-help.md / Migration-Help)
- Per-service solutions and project layouts to make each service independently buildable and testable

## Prerequisites
- .NET SDK (match the TargetFramework in each service's csproj)
- A database supported by the chosen EF Core provider (SQL Server, PostgreSQL, etc.)
- Docker Desktop (required for integration tests)
- (Optional) dotnet-ef global tool for applying migrations:
  ```
  dotnet tool install --global dotnet-ef
  ```

## Run a service (short path)
1. From repository root, restore and build:
   ```bash
   dotnet restore
   dotnet build
   ```
2. Configure required environment variables or appsettings for the service you want to run (connection strings, messaging endpoints, etc.).
3. Apply migrations where applicable (see each service's Migration-help.md / Migration-Help):
   ```bash
   cd CatalogService/src/CatalogService.Infrastructure
   dotnet ef database update --project ./CatalogService.Infrastructure.csproj --startup-project ../CatalogService.Api
   ```
   Adjust path and project names for CartService and OrderService as needed.
4. Run the API (example for one service):
   ```bash
   cd ../CatalogService.Api
   dotnet run
   ```
5. Repeat for other services; each service exposes its own API surface.

## Configuration
- Check each service's appsettings.json / appsettings.Development.json and the Migration-help.md files for service-specific configuration and migration instructions.
- Shared configuration conventions (e.g., connection string keys) are typically defined by Building-Blocks contracts—inspect `Building-Blocks/src/ShopNet.Contracts` to see shared configuration keys and DTOs.

## Development notes
- Work inside one service at a time: modify Domain → Application → Infrastructure, update migrations, then update Api and tests.
- Keep shared contracts stable: changes in `Building-Blocks/src/ShopNet.Contracts` can affect multiple services.
- Migration helper files in CatalogService and OrderService contain notes to run or scaffold EF Core migrations for those services.

## Tests

Each component keeps production projects under `src` and test projects in the adjacent `test` directory. Unit tests cover domain rules, application handlers, API/controller behavior, and infrastructure adapters. Integration tests use Testcontainers to start disposable Docker dependencies:

- Cart: Redis and RabbitMQ
- Catalog: SQL Server and RabbitMQ
- Order: SQL Server and RabbitMQ
- Identity: SQL Server

Run every test from the repository root:

```bash
dotnet test shopnet.slnx
```

Docker Desktop must be running. Testcontainers selects random host ports and removes its containers after the run, so the suites do not require fixed local ports or shared development databases.

## Contributing
- Fork and open a branch per change (one concern per PR).
- If you change domain models that affect the DB, include migration files and clear migration instructions in the PR.
- Add or update tests where applicable.


