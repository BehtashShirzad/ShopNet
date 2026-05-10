using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CartService.Api
{
    public static class ContextHelper
    {
        public static Guid GetUserId(this IHttpContextAccessor contextAccessor)
        {
                Guid.TryParse(  contextAccessor.HttpContext?.User?.FindFirst("sub")?.Value,out Guid UserId);
                return UserId;
            
        }
        
    }
}