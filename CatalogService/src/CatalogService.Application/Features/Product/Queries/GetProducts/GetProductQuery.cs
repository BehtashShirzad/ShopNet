using CatalogService.Application.Features.Product.CreateProduct;
using CatalogService.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Application.Features.Product.Queries.GetProducts;

public record GetProductsQuery : IRequest<List<CreateProductCommandResponse>>;

public class GetProductsQueryHandler (QueryDbContext queryDbContext): IRequestHandler<GetProductsQuery,List<CreateProductCommandResponse>>
{
    public async Task<List<CreateProductCommandResponse>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        return queryDbContext.Products.AsNoTracking().Select(p => new CreateProductCommandResponse( p.Name, p.Description??string.Empty,p.Price)).ToList();
    }
}
 