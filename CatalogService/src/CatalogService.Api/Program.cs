using CatalogService.Api.Endpoints;
using CatalogService.Api.Routes;
using CatalogService.Application;
using CatalogService.Infrastructure;
using Serilog;
using Application.Abstractions.Contracts;
using CatalogService.Api.Grpc;
using Microsoft.EntityFrameworkCore;
using ShopNet.Authorization;

var builder = WebApplication.CreateBuilder(args);

 
builder.Services.AddOpenApi();
builder.Services.AddShopNetSwagger("Catalog Service");
builder.Services.AddApplicationServices();
builder.Services.AddGrpc();
builder.Services.AddShopNetAuthorization(builder.Configuration, CatalogPermissions.All);
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()               
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Services.AddInfraService(builder.Configuration);
builder.Host.UseSerilog();
builder.Services.AddHttpContextAccessor();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(builder.Configuration.GetValue("ServicePorts:Grpc", 60002), o =>
    {
        o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
    options.ListenAnyIP(builder.Configuration.GetValue("ServicePorts:Http", 6002), o =>
    {
        o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
    });
});


var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<WriteDbContext>().Database.MigrateAsync();
}
 

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue("HttpsRedirection:Enabled", true))
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapGrpcService<CatalogServiceGrpcService>()
    .RequireAuthorization(CatalogPermissions.InternalRead);
app.Run();
