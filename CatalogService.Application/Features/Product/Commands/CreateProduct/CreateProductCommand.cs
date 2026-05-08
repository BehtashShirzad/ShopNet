using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Domain.Aggregates;
using CatalogService.Domain.Contracts;
using CatalogService.Infrastructure;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Application.Features.Product.CreateProduct
{

    public record CreateProductCommand(Guid  CategoryId,string Name, string Description,decimal Price):IRequest<CreateProductCommandResponse>;
    public record CreateProductCommandResponse(string Name,string Description,decimal Price);

    public class CreateProductCommandHandler(WriteDbContext dbContext,IUnitOfWork unitOfWork) : IRequestHandler<CreateProductCommand,CreateProductCommandResponse>
    {
        public async Task<CreateProductCommandResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var isExists = await dbContext.Categories.AnyAsync(_=> _.Id == request.CategoryId, cancellationToken);
            if (!isExists)
                throw new Exception("Category not found");

            var isProductNameExists = await dbContext.Products.AnyAsync(_ => _.Name == request.Name, cancellationToken);
            if (isProductNameExists)
                throw new Exception("Product name already exists");
            
           var product = ProductAggregate.Create(request.CategoryId,request.Name,request.Description,request.Price);
            await dbContext.Products.AddAsync(product);
            await dbContext.SaveChangesAsync(cancellationToken);

            await unitOfWork.PersistAsync(cancellationToken);
           var dto = product.Adapt<CreateProductCommandResponse>();
            return dto;

        }
    }
}