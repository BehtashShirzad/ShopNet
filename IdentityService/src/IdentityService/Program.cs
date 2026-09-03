
using IdentityService.Models;
using IdentityService.Persistence;
using IdentityService.Services;
using IdentityService.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityServiceConnection"));
    options.UseOpenIddict();
});

// ─── ASP.NET Core Identity ───────────────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit           = true;
    options.Password.RequiredLength         = 8;
    options.Password.RequireUppercase       = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ─── OpenIddict ──────────────────────────────────────────────────────────────
builder.Services.AddOpenIddict()

    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AppDbContext>();
    })

    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token")
               .SetAuthorizationEndpointUris("/connect/authorize")
               .SetUserinfoEndpointUris("/connect/userinfo")       
               .SetIntrospectionEndpointUris("/connect/introspect")
               .SetRevocationEndpointUris("/connect/revoke");

        options.AllowClientCredentialsFlow()
               .AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();

        options.RequireProofKeyForCodeExchange();

        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.OfflineAccess,
            "api"
        );

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(60))
               .SetRefreshTokenLifetime(TimeSpan.FromDays(14));

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else
        {
            var certificate = OpenIddictCertificateLoader.Load(builder.Configuration);

            options.AddEncryptionCertificate(certificate)
                   .AddSigningCertificate(certificate);
        }

        var aspNetCore =   options.UseAspNetCore()
            .EnableTokenEndpointPassthrough()
            .EnableAuthorizationEndpointPassthrough()
            .EnableUserinfoEndpointPassthrough();
        if (builder.Environment.IsDevelopment())
            aspNetCore.DisableTransportSecurityRequirement();

    })

    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// ─── Services ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHostedService<ClientSeeder>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo() { Title = "Identity Service", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

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
