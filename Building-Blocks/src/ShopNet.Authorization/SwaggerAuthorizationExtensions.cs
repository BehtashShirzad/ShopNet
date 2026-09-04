using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ShopNet.Authorization;

public static class SwaggerAuthorizationExtensions
{
    public const string BearerScheme = "Bearer";

    public static IServiceCollection AddShopNetSwagger(
        this IServiceCollection services,
        string title,
        string version = "v1")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(version, new OpenApiInfo
            {
                Title = title,
                Version = version
            });

            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Paste the JWT access token only. Swagger adds the Bearer prefix automatically."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document, null)] = []
            });
        });

        return services;
    }
}
