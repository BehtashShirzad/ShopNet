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
             services.AddScoped<IApplicationDbContext, WriteDbContext>();
             services.AddScoped<IProductWriteRepository,ProductWriteRepository>();
             services.AddScoped<ICategoryWriteRepository,CategoryWriteRepository>();
             services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
             services.AddScoped<IProductReadRepository, ProductReadRepository>();
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