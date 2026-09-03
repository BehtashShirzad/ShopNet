using IdentityService.Models;
using IdentityService.Persistence;
using IdentityService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityService.IntegrationTests;

[Collection(IdentitySqlServerCollection.Name)]
public class IdentityServiceIntegrationTests(IdentitySqlServerFixture fixture)
{
    [Fact]
    public async Task SeederAndAuthService_WorkEndToEndAgainstSqlServerContainer()
    {
        var accessor = new HttpContextAccessor();
        await using var provider = BuildProvider(fixture.ConnectionString, accessor);
        var seeder = new ClientSeeder(provider);

        await seeder.StartAsync(CancellationToken.None);
        await seeder.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        accessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True(await roleManager.RoleExistsAsync("Admin"));
        Assert.True(await roleManager.RoleExistsAsync("User"));
        Assert.True(await roleManager.RoleExistsAsync("Service"));
        var applicationManager = scope.ServiceProvider
            .GetRequiredService<IOpenIddictApplicationManager>();
        Assert.NotNull(await applicationManager.FindByClientIdAsync("web-client"));
        Assert.NotNull(await applicationManager.FindByClientIdAsync("order-service"));

        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var registered = await auth.RegisterAsync(
            email, "Password1", "First", "Last");

        Assert.True(registered.Success);
        Assert.NotNull(registered.UserId);
        var duplicate = await auth.RegisterAsync(
            email, "Password1", "First", "Last");
        Assert.False(duplicate.Success);

        var user = await auth.GetUserByIdAsync(registered.UserId!);
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal("First", user.FirstName);
        Assert.Contains("User", user.Roles);

        var wrongPassword = await auth.ChangePasswordAsync(
            registered.UserId!, "WrongPassword1", "NewPassword1");
        Assert.False(wrongPassword.Success);
        var changed = await auth.ChangePasswordAsync(
            registered.UserId!, "Password1", "NewPassword1");
        Assert.True(changed.Success);

        var login = await auth.LoginAsync(email, "NewPassword1");
        Assert.True(login.Success);
        Assert.True(await auth.LogoutAsync(registered.UserId!));
    }

    private static ServiceProvider BuildProvider(
        string connectionString, IHttpContextAccessor accessor)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton(accessor);
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.UseOpenIddict();
        });
        services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddOpenIddict().AddCore(options =>
            options.UseEntityFrameworkCore().UseDbContext<AppDbContext>());
        services.AddScoped<IAuthService, AuthService>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
