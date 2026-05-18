using Application.Abstractions;
using Application.Abstractions.Contracts;
using CatalogService.Domain;
using CatalogService.Domain.Entities;
using Mapster;
using MediatR;

namespace CatalogService.Application.Features.Category.Commands.CreateCategory
{
    public record CreateCategoryCommand(string Name):ICommand<CreateCategoryCommandResponse>;
     public record CreateCategoryCommandResponse(Guid Id,string Name);
    public class CreateCategoryCommandHandler (ICategoryWriteRepository writeRepository,ICategoryReadRepository categoryReadRepository): ICommandHandler<CreateCategoryCommand,CreateCategoryCommandResponse>
    {
        public async Task<CreateCategoryCommandResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            var isExist = await categoryReadRepository.CategoryExists(x => x.Name == request.Name, cancellationToken);
            if (isExist)
            {
                throw new Exception("Category with the same name already exists.");
            }

            var category = CategoryEntity.Create(request.Name);
           await writeRepository.AddCategory(category);
            return category.Adapt<CreateCategoryCommandResponse>();
        }
    }
}