using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;

namespace CatalogService.Application.Features.Category.Commands.CreateCategory
{
    public class CreateCategoryCommandValidator:AbstractValidator<CreateCategoryCommand>
    {
        public CreateCategoryCommandValidator()
        {
            RuleFor(_=>_.Name).NotNull().NotEmpty();
            
        }
        
    }
}