using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CatalogService.Domain.ExceptionMessages
{
    public class ProductExceptionMessages
    {

        public const string PriceMustBeGreaterThanZero = "Price must be greater than zero.";
        public const string NameCannotBeNullOrEmpty = "Name cannot be null or empty.";
         public const string InvalidCategoryId = "Invalid Category Id";
    }
}