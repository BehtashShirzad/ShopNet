using CatalogService.Api.Endpoints;
using CatalogService.Api.Routes;
using CatalogService.Application;
using CatalogService.Infrastructure;
using Serilog;
using Application.Abstractions.Contracts;

var builder = WebApplication.CreateBuilder(args);

 
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationServices();
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()               
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Services.AddInfraService(builder.Configuration);
builder.Host.UseSerilog();
builder.Services.AddHttpContextAccessor();


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
 
app.Run();

 