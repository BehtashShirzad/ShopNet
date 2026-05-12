using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CartService.Application.Query;

namespace CartService.Application
{
    public interface ICatalogService
    {
         Task<GetProductDto?> GetProduct(Guid productId);
    }
}