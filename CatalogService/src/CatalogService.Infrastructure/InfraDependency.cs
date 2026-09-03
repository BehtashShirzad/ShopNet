using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
 
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Application.Abstractions.Contracts;
using Application.Abstractions;
using CatalogService.Domain;
using MassTransit;

namespace CatalogService.Infrastructure
{
    public static class InfraDependency
    {
        public static void AddInfraService(this IServiceCollection services,IConfiguration configuration)
        {

            services.AddDbContext<WriteDbContext>(opt=>
            {
                opt.UseSqlServer(configuration.GetConnectionString("CatalogServiceConnection"));
            });
           services.AddDbContext<QueryDbContext>(opt=>
            {
                opt.UseSqlServer(configuration.GetConnectionString("CatalogServiceConnection"));
            });
           services.AddScoped<ICurrentUser, CurrentUser>();
           services.AddScoped<IDomainEventBus, DomainEventBus>();
           services.AddScoped<IIntegrationEventBus, Bus>();
             services.AddScoped<IDomainEventDispatcher, MediatrDomainEventDispatcher>();
             services.AddScoped<IApplicationDbContext>(provider =>
                 provider.GetRequiredService<WriteDbContext>());
             services.AddScoped<IProductWriteRepository,ProductWriteRepository>();
             services.AddScoped<ICategoryWriteRepository,CategoryWriteRepository>();
             services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
             services.AddScoped<IProductReadRepository, ProductReadRepository>();
             services.AddMassTransit(x =>
             {
                 x.AddEntityFrameworkOutbox<WriteDbContext>(outbox =>
                 {
                     outbox.UseSqlServer();
                     outbox.UseBusOutbox(delivery =>
                     {
                         // Stage 1 must retain events until Inventory has a durable subscription.
                         if (!bool.TryParse(configuration["CatalogOutbox:DeliveryEnabled"], out var enabled)
                             || !enabled)
                             delivery.DisableDeliveryService();
                     });
                     outbox.QueryDelay = TimeSpan.FromSeconds(1);
                 });
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
