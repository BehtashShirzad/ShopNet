using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
           
        }
        
    }
}