using CatalogService.Application.Features.Product.Commands.CreateProduct;
using CatalogService.Application.Features.Product.CreateProduct;
using CatalogService.Domain;
 
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Application.Features.Product.Queries.GetProducts;

public record GetProductsQuery : IRequest<List<CreateProductCommandResponse>>;

public class GetProductsQueryHandler (IProductReadRepository productReadRepository): IRequestHandler<GetProductsQuery,List<CreateProductCommandResponse>>
{
    public  async Task<List<CreateProductCommandResponse>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var   products = await productReadRepository.GetProductsAsync();// TODO: Projection Needed
        return products.Adapt<List<CreateProductCommandResponse>>();


    }
}
 