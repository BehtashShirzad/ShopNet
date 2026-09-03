using Application.Abstractions;
using Application.Abstractions.Contracts;
using CatalogService.Domain;
using CatalogService.Domain.Aggregates;
using Mapster;

namespace CatalogService.Application.Features.Product.Commands.CreateProduct
{

    public record CreateProductCommand(Guid CategoryId, string Name, string Description, decimal Price)
    :ICommand<CreateProductCommandResponse>;
    public record CreateProductCommandResponse(Guid Id,string Name,string Description,decimal Price);

    public class CreateProductCommandHandler(IProductWriteRepository productWriteRepository,IProductReadRepository productReadRepository ) : ICommandHandler<CreateProductCommand,CreateProductCommandResponse>
    {
        public async Task<CreateProductCommandResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
          
            var validationData =await productReadRepository.ValidateProductExists(request.Name, request.CategoryId,cancellationToken);
            if (!validationData.CategoryExsits)
                throw new Exception($"Category '{request.CategoryId}' not found");

            if (validationData.ProductNameCount > 0)
             throw new Exception($"Product '{request.Name}' already exists in this category");

            var product = ProductAggregate.Create(request.CategoryId, request.Name, request.Description, request.Price);
           productWriteRepository.AddProduct(product);
          
             
           var dto = product.Adapt<CreateProductCommandResponse>();
            return dto;

        }
    }
}
