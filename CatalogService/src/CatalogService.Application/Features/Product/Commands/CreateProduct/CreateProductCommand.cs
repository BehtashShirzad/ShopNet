using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Domain.Aggregates;
 
using CatalogService.Infrastructure;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Application.Abstractions;

namespace CatalogService.Application.Features.Product.CreateProduct
{

    public record CreateProductCommand(Guid  CategoryId,string Name, string Description,decimal Price)
    :ICommand<CreateProductCommandResponse>;
    public record CreateProductCommandResponse(string Name,string Description,decimal Price);

    public class CreateProductCommandHandler(WriteDbContext dbContext ) : IRequestHandler<CreateProductCommand,CreateProductCommandResponse>
    {
        public async Task<CreateProductCommandResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var validationData = await dbContext.Categories
            .AsNoTracking()
            .Where(c => c.Id == request.CategoryId)
            .Select(c => new 
            { 
                CategoryExists = true,
                ProductNameCount = dbContext.Products
                    .AsNoTracking()
                    .Count(p => p.Name == request.Name && p.CategoryId == request.CategoryId)
            })
            .FirstOrDefaultAsync(cancellationToken);
              if (validationData == null)
            throw new Exception($"Category '{request.CategoryId}' not found");

        if (validationData.ProductNameCount > 0)
            throw new Exception($"Product '{request.Name}' already exists in this category");

           var product = ProductAggregate.Create(request.CategoryId,request.Name,request.Description,request.Price);
            await dbContext.Products.AddAsync(product);
          
             
           var dto = product.Adapt<CreateProductCommandResponse>();
            return dto;

        }
    }
}