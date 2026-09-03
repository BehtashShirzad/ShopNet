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
COPY ["CartService/src/CartService.Domain/CartService.Domain.csproj", "CartService/src/CartService.Domain/"]
COPY ["CartService/src/CartService.Application/CartService.Application.csproj", "CartService/src/CartService.Application/"]
COPY ["CartService/src/CartService.Infrastructure/CartService.Infrastructure.csproj", "CartService/src/CartService.Infrastructure/"]
COPY ["CartService/src/CartService.Api/CartService.Api.csproj", "CartService/src/CartService.Api/"]
RUN --mount=type=cache,id=shopnet-nuget,target=/root/.nuget/packages \
    dotnet restore "CartService/src/CartService.Api/CartService.Api.csproj"

COPY . .
RUN --mount=type=cache,id=shopnet-nuget,target=/root/.nuget/packages \
    dotnet publish "CartService/src/CartService.Api/CartService.Api.csproj" \
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
ENTRYPOINT ["dotnet", "CartService.Api.dll"]
