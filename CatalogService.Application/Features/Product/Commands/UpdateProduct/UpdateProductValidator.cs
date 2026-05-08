using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;

namespace CatalogService.Application.Features.Product.Commands.UpdateProduct
{
    public class UpdateProductValidator:AbstractValidator<UpdateProductCommand>
    {
        public UpdateProductValidator()
        {
           
           
         RuleFor(_=>_.ProductId).NotEmpty().NotNull();
            
        }
        
    }
}