using InventoryService.Api.Grpc;
using InventoryService.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
builder.Services.AddInventory(builder.Configuration);
var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
}

app.MapGrpcService<InventoryAvailabilityGrpcService>();
app.Run();

public partial class Program;
