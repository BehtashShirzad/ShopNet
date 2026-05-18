using CartService.Api;
using CartService.Application;
using CartService.Infrastructure;
using Serilog;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationServices();
 builder.Services.AddInfraServices(builder.Configuration);
 
 Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()               
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
 
 builder.Services.AddHttpContextAccessor();
 builder.Host.UseSerilog();
 builder.WebHost.ConfigureKestrel(options =>
 {
     options.ListenLocalhost(60003, o =>
     {
         o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
     });
     options.ListenLocalhost(6003, o =>
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
app.MapCartEndpoints();


app.Run();

 
