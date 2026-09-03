using InventoryService.Api.Grpc;
using InventoryService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
builder.Services.AddInventory(builder.Configuration);
var app = builder.Build();
app.MapGrpcService<InventoryAvailabilityGrpcService>();
app.Run();

public partial class Program;
