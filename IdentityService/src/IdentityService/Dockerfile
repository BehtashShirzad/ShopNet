# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip

COPY ["Directory.Build.targets", "."]
COPY ["IdentityService/src/IdentityService/IdentityService.csproj", "IdentityService/src/IdentityService/"]
RUN --mount=type=cache,id=shopnet-nuget,target=/root/.nuget/packages \
    dotnet restore "IdentityService/src/IdentityService/IdentityService.csproj"

COPY . .
RUN --mount=type=cache,id=shopnet-nuget,target=/root/.nuget/packages \
    dotnet publish "IdentityService/src/IdentityService/IdentityService.csproj" \
    --configuration "$BUILD_CONFIGURATION" --output /app/publish --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    OpenIddict__Certificate__Path=/run/secrets/openiddict.pfx \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
RUN chown -R "$APP_UID:$APP_UID" /app
USER $APP_UID
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "IdentityService.dll"]
