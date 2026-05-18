using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Abstractions.Contracts;
using Domain.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Domain;

namespace OrderService.Infrastructure
{
    public static class DependencyServices
    {
        public static void 
         AddInfrastructureServices(this IServiceCollection services,
         IConfiguration configuration)
        {
            services.AddDbContext<WriteDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("OrderServiceConnection")));
                services.AddScoped<IUnitOfWork, WriteDbContext>();
                
                services.AddScoped<IOrderRepository, OrderRepository>();
                 services.AddScoped<IDomainEventDispatcher, MediatrDomainEventDispatcher>();
                 services.AddScoped<IDomainEventBus, DomainEventBus>();
           services.AddScoped<IIntegrationEventBus,Bus>();
           services.AddScoped<IApplicationDbContext, WriteDbContext>();
           services.AddMassTransit(x =>
           {
                    
               x.AddConsumers(typeof(Application.DependencyInjection).Assembly);
               x.SetKebabCaseEndpointNameFormatter();
               x.UsingRabbitMq((context, rcfg) =>
               {
                   var rabbit = configuration.GetSection("RabbitMq");
                   
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