 
using IdentityService.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IdentityService.Services;

/// <summary>
/// هر بار که سرویس بالا میاد، client‌های OAuth و Role های پایه رو seed می‌کنه.
/// </summary>
public class ClientSeeder : IHostedService
{
    private readonly IServiceProvider _services;

    public ClientSeeder(IServiceProvider services) => _services = services;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();

        // اطمینان از اعمال Migrations
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        // Seed Roles
        await SeedRolesAsync(scope.ServiceProvider);

        // Seed OAuth Clients
        await SeedClientsAsync(scope.ServiceProvider, cancellationToken);
    }

    private static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles  = ["Admin", "User", "Service"];

        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
    }

    private static async Task SeedClientsAsync(IServiceProvider sp, CancellationToken ct)
    {
        var manager = sp.GetRequiredService<IOpenIddictApplicationManager>();

        // ── 1) Web/SPA Client — Authorization Code Flow ──────────────────────
        if (await manager.FindByClientIdAsync("web-client", ct) is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId     = "web-client",
                ClientSecret = "web-client-secret-change-in-prod",
                DisplayName  = "Web Application",
                ClientType   = ClientTypes.Confidential,

                RedirectUris =
                {
                    new Uri("https://localhost:5002/callback"),
                    new Uri("https://localhost:5002/signin-oidc")
                },
                PostLogoutRedirectUris =
                {
                    new Uri("https://localhost:5002/signout-callback-oidc")
                },

                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.Logout,

                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,

                    Permissions.ResponseTypes.Code,

                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles,
                    $"{Permissions.Prefixes.Scope}api",
                    $"{Permissions.Prefixes.Scope}offline_access",
                }
            }, ct);
        }

        // ── 2) Service Client — Client Credentials Flow ───────────────────────
        // هر میکروسرویس که نیاز داره با سرویس‌های دیگه حرف بزنه
        if (await manager.FindByClientIdAsync("order-service", ct) is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId     = "order-service",
                ClientSecret = "order-service-secret-change-in-prod",
                DisplayName  = "Order Microservice",
                ClientType   = ClientTypes.Confidential,

                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    $"{Permissions.Prefixes.Scope}api",
                }
            }, ct);
        }

        // ── می‌تونی client‌های بیشتری اضافه کنی ──────────────────────────────
        // if (await manager.FindByClientIdAsync("inventory-service", ct) is null) { ... }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
