using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using CatalogService.Domain;

using Microsoft.EntityFrameworkCore;

namespace CatalogService.Application.Features.Product.Queries.GetProduct
{

    public record GetProductDto(Guid Id, string Name, decimal Price);
    public record GetProductQuery(Guid Id):IQuery<GetProductDto?>;
    public class GetProductQueryHandler(IProductReadRepository productReadRepository) : IQueryHandler<GetProductQuery, GetProductDto?>
    {
        public  async Task<GetProductDto?> Handle(GetProductQuery request, CancellationToken cancellationToken)
        {
            var product = await productReadRepository.GetProductAsync(p => p.Id == request.Id,cancellationToken);//TODO: NEED Projection
           
            return product is null
                ? null
                : new GetProductDto(product.Id, product.Name, product.Price);
        }
    }
     
}
