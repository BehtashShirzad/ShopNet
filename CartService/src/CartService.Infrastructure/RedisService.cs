using StackExchange.Redis;

namespace CartService.Infrastructure;

public class RedisService(IConnectionMultiplexer connectionMultiplexer) : IRedisService
{
     IConnectionMultiplexer _connectionMultiplexer = connectionMultiplexer;
        IDatabase _database => _connectionMultiplexer.GetDatabase();
    public async Task<string?> GetValue(string key)
    {
       return await _database.StringGetAsync(key);
    }

    public async Task SetValue(string key, string value, TimeSpan? expiry = null)
    {
         if (expiry.HasValue)
             await _database.StringSetAsync(key, value, expiry.Value);
        else
             await _database.StringSetAsync(key, value);
        
    }
}
