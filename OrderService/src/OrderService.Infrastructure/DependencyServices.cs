using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
           
        }
        
    }
}