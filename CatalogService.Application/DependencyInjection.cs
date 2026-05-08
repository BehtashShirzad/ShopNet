using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Application.Pipelines;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogService.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            var assembly = typeof(DependencyInjection);
            services.AddValidatorsFromAssembly(assembly.Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
                services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlerBehavior<,>));
            services.AddMediatR(cg =>
            {
                cg.RegisterServicesFromAssemblies(assembly.Assembly);
            });

            return services;
        }
        
    }
}