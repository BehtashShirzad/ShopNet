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
                Guid.TryParse( 
                     contextAccessor.HttpContext?.User?.FindFirst("sub")
                ?.Value??"00000000-0000-0000-0000-000000000011",out Guid UserId);
                return UserId;
            
        }
        
    }
}