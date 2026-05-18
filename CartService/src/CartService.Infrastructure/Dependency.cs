using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using CartService.Domain;
using CatalogService.API.Grpc.Protos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Grpc.Net.Client;
using CartService.Application;
using MassTransit;

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

            var catalogServiceAddress = cfg["Grpc:CatalogService"]??"http://localhost:6002";
            services.AddGrpcClient<CatalogProtoService.CatalogProtoServiceClient>(o =>
                {
                    o.Address = new Uri(catalogServiceAddress);
                });
                
                services.AddScoped<ICatalogService, CatalogGrpcClient>();
                services.AddScoped<IIntegrationEventBus,Bus>();
                services.AddMassTransit(x =>
                {
                    x.AddConsumers(typeof(Application.DependencyInjection).Assembly);
                    x.SetKebabCaseEndpointNameFormatter();
                    
                    x.UsingRabbitMq((context, rcfg) =>
                    {
                        var rabbit = cfg.GetSection("RabbitMq");
                        
                        rcfg.Host(
                            rabbit["Host"],
                            ushort.Parse(rabbit["Port"]),
                            rabbit["VirtualHost"],
                            h =>
                            {
                                h.Username(rabbit["Username"]);
                                h.Password(rabbit["Password"]);
                            });

                        rcfg.ConfigureEndpoints(context);
                    });
                });

        }
        
    }
}