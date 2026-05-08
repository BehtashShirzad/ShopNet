using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
 
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Application.Abstractions.Contracts;
using Application.Abstractions;
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
           services.AddScoped<IBus, Bus>();
 services.AddScoped<IDomainEventDispatcher, MediatrDomainEventDispatcher>();

        }
        
    }
}