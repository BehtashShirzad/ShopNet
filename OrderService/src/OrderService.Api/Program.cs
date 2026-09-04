
using OrderService.Api;
using OrderService.Application;
using OrderService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ShopNet.Authorization;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()               
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddShopNetSwagger("Order Service");
builder.Host.UseSerilog();
builder.Services.AddHttpContextAccessor();
builder.Services.AddShopNetAuthorization(builder.Configuration, OrderPermissions.All);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(builder.Configuration.GetValue("ServicePorts:Grpc", 60001), o =>
    {
        o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
    options.ListenAnyIP(builder.Configuration.GetValue("ServicePorts:Http", 6001), o =>
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

app.MapEndpoint();
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
app.Run();
