using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CartService.Infrastructure
{
    public interface IRedisService
    {
       public Task<string?> GetValue(string key);
       public Task SetValue(string key, string value, TimeSpan? expiry = null);
    }
}
