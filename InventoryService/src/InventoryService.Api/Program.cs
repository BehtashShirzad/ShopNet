using InventoryService.Api.Grpc;
using InventoryService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ShopNet.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
builder.Services.AddInventory(builder.Configuration);
builder.Services.AddShopNetAuthorization(builder.Configuration, InventoryPermissions.All);
builder.Services.AddShopNetSwagger("Inventory Service");
var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapGrpcService<InventoryAvailabilityGrpcService>()
    .RequireAuthorization(InventoryPermissions.InternalRead);
app.Run();

public partial class Program;
