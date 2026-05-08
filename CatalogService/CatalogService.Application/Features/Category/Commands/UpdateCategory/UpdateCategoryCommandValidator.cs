using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Application.Features.Product.Commands.UpdateProduct;
using FluentValidation;

namespace CatalogService.Application.Features.Category.Commands.UpdateCategory
{
    public class UpdateCategoryCommandValidator:AbstractValidator<UpdateCategoryCommand>
    {
        public UpdateCategoryCommandValidator()
        {
            
            RuleFor(_=>_.NewName).NotEmpty().NotNull();
            RuleFor(_=>_.Id).NotEmpty().NotNull();
        }

        
    }
}