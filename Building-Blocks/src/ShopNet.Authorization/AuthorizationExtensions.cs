using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ShopNet.Authorization;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddShopNetAuthorization(
        this IServiceCollection services,
        IConfiguration configuration,
        params string[] permissions)
    {
        var section = configuration.GetRequiredSection("Keycloak");
        var authority = section["Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority is required.");
        var audience = section["Audience"]
            ?? throw new InvalidOperationException("Keycloak:Audience is required.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                var metadataAddress = section["MetadataAddress"];
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                    options.MetadataAddress = metadataAddress;
                options.RequireHttpsMetadata = section.GetValue("RequireHttpsMetadata", true);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidAudience = audience,
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles"
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
                options.AddPolicy(permission, policy =>
                    policy.RequireAuthenticatedUser().RequireClaim(ShopNetClaims.Permission, permission));
        });
        return services;
    }
}

public static class ShopNetClaims
{
    public const string Permission = "permissions";
}
