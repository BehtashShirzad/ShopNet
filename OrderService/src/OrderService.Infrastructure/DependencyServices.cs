using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;
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
           
        }
        
    }
}