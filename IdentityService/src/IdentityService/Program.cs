using IdentityService.Services;
using Keycloak.Client;
using Keycloak.Client.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var keycloakOptions = builder.Configuration
    .GetSection(KeycloakOptions.SectionName)
    .Get<KeycloakOptions>()
    ?? throw new InvalidOperationException("The Keycloak configuration section is required.");

builder.Services.AddSingleton(keycloakOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IKeycloakClient, KeycloakClient>();
builder.Services.AddScoped<IIdentityProvider, KeycloakIdentityProvider>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakOptions.Authority ??
            $"{keycloakOptions.BaseUrl.TrimEnd('/')}/realms/{keycloakOptions.Realm}";
        if (!string.IsNullOrWhiteSpace(keycloakOptions.MetadataAddress))
            options.MetadataAddress = keycloakOptions.MetadataAddress;
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidAudience = keycloakOptions.Audience,
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Identity Service",
        Version = "v1"
    });
});

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
