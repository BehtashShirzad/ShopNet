# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip

COPY ["Directory.Build.targets", "."]
COPY ["Building-Blocks/src/Domain.Abstractions/Domain.Abstractions.csproj", "Building-Blocks/src/Domain.Abstractions/"]
COPY ["Building-Blocks/src/ShopNet.Contracts/ShopNet.Contracts.csproj", "Building-Blocks/src/ShopNet.Contracts/"]
COPY ["Building-Blocks/src/ShopNet.Authorization/ShopNet.Authorization.csproj", "Building-Blocks/src/ShopNet.Authorization/"]
COPY ["InventoryService/src/InventoryService.Domain/InventoryService.Domain.csproj", "InventoryService/src/InventoryService.Domain/"]
COPY ["InventoryService/src/InventoryService.Application/InventoryService.Application.csproj", "InventoryService/src/InventoryService.Application/"]
COPY ["InventoryService/src/InventoryService.Infrastructure/InventoryService.Infrastructure.csproj", "InventoryService/src/InventoryService.Infrastructure/"]
COPY ["InventoryService/src/InventoryService.Api/InventoryService.Api.csproj", "InventoryService/src/InventoryService.Api/"]
RUN --mount=type=cache,id=shopnet-nuget-v2,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "InventoryService/src/InventoryService.Api/InventoryService.Api.csproj"

COPY . .
RUN --mount=type=cache,id=shopnet-nuget-v2,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "InventoryService/src/InventoryService.Api/InventoryService.Api.csproj" \
    --configuration "$BUILD_CONFIGURATION" --output /app/publish --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    Kestrel__Endpoints__Grpc__Url=http://0.0.0.0:8080 \
    Kestrel__Endpoints__Grpc__Protocols=Http2 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
RUN chown -R "$APP_UID:$APP_UID" /app
USER $APP_UID
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "InventoryService.Api.dll"]
