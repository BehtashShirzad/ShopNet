using StackExchange.Redis;

namespace CartService.Infrastructure;

public class RedisService(IConnectionMultiplexer connectionMultiplexer) : IRedisService
{
    
         private readonly IConnectionMultiplexer _connectionMultiplexer = connectionMultiplexer;
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    public async Task<string?> GetValue(string key)
    {
       return await _database.StringGetAsync(key);
    }

    public async Task SetValue(string key, string value, TimeSpan? expiry = null)
    {
     // change if you want 
     if (!expiry.HasValue)
          expiry = TimeSpan.FromHours(24);
     //
         if (expiry.HasValue)
             await _database.StringSetAsync(key, value, expiry.Value);
        else
             await _database.StringSetAsync(key, value);
        
    }
}
