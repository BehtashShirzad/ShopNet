using IdentityService.Services;
using Keycloak.Client;
using Keycloak.Client.Configuration;
using ShopNet.Authorization;

var builder = WebApplication.CreateBuilder(args);

var keycloakOptions = builder.Configuration
    .GetSection(KeycloakOptions.SectionName)
    .Get<KeycloakOptions>()
    ?? throw new InvalidOperationException("The Keycloak configuration section is required.");

builder.Services.AddSingleton(keycloakOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IKeycloakClient, KeycloakClient>();
builder.Services.AddScoped<IIdentityProvider, KeycloakIdentityProvider>();

builder.Services.AddShopNetAuthorization(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddShopNetSwagger("Identity Service");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
