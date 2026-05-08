using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Domain.Entities;
using CatalogService.Infrastructure;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Application.Abstractions;

namespace CatalogService.Application.Features.Category.Commands
{
    public record CreateCategoryCommand(string Name):ICommand<CreateCategoryCommandResponse>;
     public record CreateCategoryCommandResponse(Guid Id,string Name);
    public class CreateCategoryCommandHandler (WriteDbContext writeDbContext): IRequestHandler<CreateCategoryCommand,CreateCategoryCommandResponse>
    {
        public async Task<CreateCategoryCommandResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            var isExist = await writeDbContext.Categories.AnyAsync(x => x.Name == request.Name, cancellationToken);
            if (isExist)
            {
                throw new Exception("Category with the same name already exists.");
            }

           var category = CategoryEntity.Create(request.Name);
           await writeDbContext.Categories.AddAsync(category, cancellationToken);
           return category.Adapt<CreateCategoryCommandResponse>();
        }
    }
}