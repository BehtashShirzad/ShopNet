# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip

COPY ["Directory.Build.targets", "."]
COPY ["Building-Blocks/src/Domain.Abstractions/Domain.Abstractions.csproj", "Building-Blocks/src/Domain.Abstractions/"]
COPY ["Building-Blocks/src/Application.Abstractions/Application.Abstractions.csproj", "Building-Blocks/src/Application.Abstractions/"]
COPY ["Building-Blocks/src/ShopNet.Contracts/ShopNet.Contracts.csproj", "Building-Blocks/src/ShopNet.Contracts/"]
COPY ["Building-Blocks/src/ShopNet.Authorization/ShopNet.Authorization.csproj", "Building-Blocks/src/ShopNet.Authorization/"]
COPY ["OrderService/src/OrderService.Domain/OrderService.Domain.csproj", "OrderService/src/OrderService.Domain/"]
COPY ["OrderService/src/OrderService.Application/OrderService.Application.csproj", "OrderService/src/OrderService.Application/"]
COPY ["OrderService/src/OrderService.Infrastructure/OrderService.Infrastructure.csproj", "OrderService/src/OrderService.Infrastructure/"]
COPY ["OrderService/src/OrderService.Api/OrderService.Api.csproj", "OrderService/src/OrderService.Api/"]
RUN --mount=type=cache,id=shopnet-nuget-v2,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "OrderService/src/OrderService.Api/OrderService.Api.csproj"

COPY . .
RUN --mount=type=cache,id=shopnet-nuget-v2,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "OrderService/src/OrderService.Api/OrderService.Api.csproj" \
    --configuration "$BUILD_CONFIGURATION" --output /app/publish --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ServicePorts__Http=8080 \
    ServicePorts__Grpc=8081 \
    HttpsRedirection__Enabled=false \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080 8081
RUN mkdir -p /app/logs && chown -R "$APP_UID:$APP_UID" /app
USER $APP_UID
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "OrderService.Api.dll"]
