using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CartService.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CartService.Infrastructure
{
    public static class Dependency
    {
        public static void AddInfraServices(this IServiceCollection services,IConfiguration cfg)
        {
            services.AddScoped<IRedisService, RedisService>();
            services.AddScoped<IRepository, CartServiceRepository>();
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = ConfigurationOptions.Parse(cfg["Redis:ConnectionString"]??"localhost:6379", true);
                return ConnectionMultiplexer.Connect(configuration);
            });
        }
        
    }
}