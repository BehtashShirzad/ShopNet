using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using CatalogService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Application.Features.Product.Queries.GetProduct
{

    public record GetProductDto(Guid Id,string Name,decimal Price);
    public record GetProductQuery(Guid Id):IQuery<GetProductDto?>;
    public class GetProductQueryHandler(QueryDbContext queryDbContext) : IQueryHandler<GetProductQuery, GetProductDto?>
    {
        public  async Task<GetProductDto?> Handle(GetProductQuery request, CancellationToken cancellationToken)
        {
            return await queryDbContext.Products.AsNoTracking().Where(p => p.Id == request.Id)
                .Select(p => new GetProductDto(p.Id, p.Name, p.Price)).FirstOrDefaultAsync(cancellationToken);
                
            
        }
    }
     
}