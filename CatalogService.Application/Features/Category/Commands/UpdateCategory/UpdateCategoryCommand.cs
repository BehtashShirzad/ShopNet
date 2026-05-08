using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Domain.Entities;
using MediatR;

namespace CatalogService.Application.Features.Category.Commands.UpdateCategory
{
    public record UpdateCategoryCommand(Guid Id,string NewName):IRequest<bool>;

    public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, bool>
    {
        public async Task<bool> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = CategoryEntity.Create("ss");

            category.Update(request.NewName);

            return true;
        }
    }

}