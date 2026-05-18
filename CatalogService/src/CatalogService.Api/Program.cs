using CatalogService.Api.Endpoints;
using CatalogService.Api.Routes;
using CatalogService.Application;
using CatalogService.Infrastructure;
using Serilog;
using Application.Abstractions.Contracts;
using CatalogService.Api.Grpc;

var builder = WebApplication.CreateBuilder(args);

 
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationServices();
builder.Services.AddGrpc();
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
    options.ListenLocalhost(60002, o =>
    {
        o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
    options.ListenLocalhost(6002, o =>
    {
        o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
    });
});


var app = builder.Build();
 

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapGrpcService<CatalogServiceGrpcService>(); 
app.Run();

 