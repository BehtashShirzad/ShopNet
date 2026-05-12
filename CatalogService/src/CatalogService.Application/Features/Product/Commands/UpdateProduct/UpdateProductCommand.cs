using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CatalogService.Domain.Aggregates;
using MediatR;
using Application.Abstractions;
using Domain.Abstractions;
using CatalogService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Application.Features.Product.Commands.UpdateProduct
{
    public record UpdateProductCommand(Guid ProductId,Guid? CategoryId, string? NewName, decimal? Price, string? Description)
    : ICommand<bool>;
    public class UpdateProductCommandHandler(WriteDbContext context) : IRequestHandler<UpdateProductCommand, bool>
    {
        public async Task<bool> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
           var product=await context.Products.Where(p => p.Id == request.ProductId).FirstOrDefaultAsync();
            if (product == null)
                throw new Exception("Product not found");
            product.Update(request.CategoryId,request.NewName, request.Price, request.Description);

            return true;
        }
    }

}