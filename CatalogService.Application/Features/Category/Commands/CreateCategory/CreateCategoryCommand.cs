using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Domain.Entities;
using Mapster;
using MediatR;

namespace CatalogService.Application.Features.Category.Commands
{
    public record CreateCategoryCommand(string Name):IRequest<CreateCategoryCommandResponse>;
     public record CreateCategoryCommandResponse(Guid Id,string Name);
    public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand,CreateCategoryCommandResponse>
    {
        public async Task<CreateCategoryCommandResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
           var category = CategoryEntity.Create(request.Name);
           return category.Adapt<CreateCategoryCommandResponse>();
        }
    }
}