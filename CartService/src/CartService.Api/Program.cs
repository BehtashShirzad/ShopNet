using CartService.Api;
using CartService.Application;
using CartService.Infrastructure;
using Serilog;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationServices();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme =
        OpenIddict.Validation.AspNetCore
            .OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});
builder.Services.AddAuthorization();
 builder.Services.AddInfraServices(builder.Configuration);
 builder.Services.AddOpenIddict()
     .AddValidation(options =>
     {
         options.SetIssuer(builder.Configuration["IdentityService:Address"]!); // IdentityService
         options.AddAudiences("api");

         options.UseSystemNetHttp();
         options.UseAspNetCore();
     });
 Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()               
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
 
 builder.Services.AddHttpContextAccessor();
 builder.Host.UseSerilog();
 builder.WebHost.ConfigureKestrel(options =>
 {
     options.ListenAnyIP(builder.Configuration.GetValue("ServicePorts:Grpc", 60003), o =>
     {
         o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
     });
     options.ListenAnyIP(builder.Configuration.GetValue("ServicePorts:Http", 6003), o =>
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

if (app.Configuration.GetValue("HttpsRedirection:Enabled", true))
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapCartEndpoints();


app.Run();

 
