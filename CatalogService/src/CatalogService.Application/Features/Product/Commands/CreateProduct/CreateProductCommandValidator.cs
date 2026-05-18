using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Application.Features.Product.Commands.CreateProduct;
using FluentValidation;

namespace CatalogService.Application.Features.Product.CreateProduct
{
    public class CreateProductCommandValidator:AbstractValidator<CreateProductCommand>
    {
        public CreateProductCommandValidator()
        {

            RuleFor(x=>x.Name)
            .NotEmpty()
            .NotNull();

            RuleFor(x=>x.Price)
            .GreaterThan(0);
            
            RuleFor(x=>x.CategoryId).NotNull().NotEmpty();
        }
        
    }
}