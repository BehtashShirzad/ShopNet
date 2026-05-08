using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Domain.Aggregates;
using Mapster;
using MediatR;

namespace CatalogService.Application.Features.Product.CreateProduct
{

    public record CreateProductCommand(Guid  CategoryId,string Name, string Description,decimal Price):IRequest<CreateProductCommandResponse>;
    public record CreateProductCommandResponse(string Name,string Description,decimal Price);

    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand,CreateProductCommandResponse>
    {
        public async Task<CreateProductCommandResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            //? Check Category
           var product = ProductAggregate.Create(request.CategoryId,request.Name,request.Description,request.Price);
           var dto = product.Adapt<CreateProductCommandResponse>();
            return dto;

        }
    }
}