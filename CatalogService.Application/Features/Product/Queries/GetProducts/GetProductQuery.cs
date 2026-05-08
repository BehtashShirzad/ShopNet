using CatalogService.Application.Features.Product.CreateProduct;
using MediatR;

namespace CatalogService.Application.Features.Product.Queries.GetProducts;

public record GetProductQuery : IRequest<CreateProductCommandResponse>;

public class GetProductQueryHandler : IRequestHandler<GetProductQuery,CreateProductCommandResponse>
{
    public async Task<CreateProductCommandResponse> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        return new CreateProductCommandResponse( "GetProductQuery","",2);
    }
}
 