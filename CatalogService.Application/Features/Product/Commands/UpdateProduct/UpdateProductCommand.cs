using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CatalogService.Domain.Aggregates;
using MediatR;
using SharedKernel.Domain;

namespace CatalogService.Application.Features.Product.Commands.UpdateProduct
{
    public record UpdateProductCommand(Guid ProductId,Guid? CategoryId, string? NewName, decimal? Price, string? Description)
    : IRequest<bool>;
    public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, bool>
    {
        public async Task<bool> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            //TODO: Fetch From DB
            var pr = ProductAggregate.Create(IdGenerator.New(),"ss", "ss", 222);
            pr.Update(request.CategoryId,request.NewName, request.Price, request.Description);

            return true;
        }
    }

}