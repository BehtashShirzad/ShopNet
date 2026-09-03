using StackExchange.Redis;

namespace CartService.Infrastructure;

public sealed class CartRedisOptions
{
    public string KeyPrefix { get; init; } = "";
    public TimeSpan ActiveCartTtl { get; init; } = TimeSpan.FromHours(24);
    public string CartKey(Guid id) => KeyPrefix + id;
    public string MessagesKey => KeyPrefix + "cart:checkout:messages";
    public string PendingKey => KeyPrefix + "cart:checkout:pending";
    public string LeasesKey => KeyPrefix + "cart:checkout:leases";
}
public sealed record CheckoutLease(Guid EventId, string Payload, string Token);
public interface ICartRedisPersistence
{
    Task<string?> ReadAsync(Guid cartId);
    Task<bool> SaveAsync(Guid cartId, string? expected, string value);
    Task<bool> CheckoutAsync(Guid cartId, string expected, string value, Guid eventId, string message, CancellationToken ct);
}
public interface ICheckoutOutbox
{
    Task<CheckoutLease?> ClaimAsync(CancellationToken ct);
    Task<bool> AcknowledgeAsync(CheckoutLease lease, CancellationToken ct);
    Task RetryAsync(CheckoutLease lease, CancellationToken ct);
}

public sealed class CartRedisPersistence(IConnectionMultiplexer connection, CartRedisOptions options)
    : ICartRedisPersistence, ICheckoutOutbox
{
    private IDatabase Database => connection.GetDatabase();
    private static readonly string CheckTypes = """
        local function checktype(key, expected)
            local actual = redis.call('TYPE', key).ok
            if actual ~= 'none' and actual ~= expected then
                error('Unexpected checkout key type: ' .. key)
            end
        end
        """;
    private static readonly string CheckoutScript = CheckTypes + """
        
        checktype(KEYS[1], 'string')
        checktype(KEYS[2], 'hash')
        checktype(KEYS[3], 'zset')
        if redis.call('GET', KEYS[1]) ~= ARGV[1] then return 0 end
        if redis.call('HEXISTS', KEYS[2], ARGV[3]) == 1 then
            return redis.error_reply('Checkout event ID already exists')
        end
        -- All type/argument checks precede writes: Lua errors do not roll back earlier commands.
        redis.call('HSET', KEYS[2], ARGV[3], ARGV[4])
        redis.call('ZADD', KEYS[3], 0, ARGV[3])
        redis.call('SET', KEYS[1], ARGV[2])
        redis.call('PERSIST', KEYS[2])
        redis.call('PERSIST', KEYS[3])
        return 1
        """;
    private const string SaveScript = """
        local current = redis.call('GET', KEYS[1])
        if ARGV[1] == '0' then
            if current then return 0 end
        elseif current ~= ARGV[2] then return 0 end
        redis.call('SET', KEYS[1], ARGV[3], 'PX', ARGV[4])
        return 1
        """;
    private static readonly string ClaimScript = CheckTypes + """
        
        checktype(KEYS[1], 'hash')
        checktype(KEYS[2], 'zset')
        checktype(KEYS[3], 'hash')
        local time = redis.call('TIME')
        local now = time[1] * 1000 + math.floor(time[2] / 1000)
        local ids = redis.call('ZRANGEBYSCORE', KEYS[2], '-inf', now, 'LIMIT', 0, 1)
        if #ids == 0 then return nil end
        local payload = redis.call('HGET', KEYS[1], ids[1])
        if not payload then return redis.error_reply('Pending checkout payload missing') end
        redis.call('HSET', KEYS[3], ids[1], ARGV[1])
        redis.call('ZADD', KEYS[2], now + 60000, ids[1])
        return { ids[1], payload, ARGV[1] }
        """;
    private static readonly string AckScript = CheckTypes + """
        
        checktype(KEYS[1], 'hash')
        checktype(KEYS[2], 'zset')
        checktype(KEYS[3], 'hash')
        if redis.call('HGET', KEYS[3], ARGV[1]) ~= ARGV[2] then return 0 end
        redis.call('HDEL', KEYS[1], ARGV[1])
        redis.call('ZREM', KEYS[2], ARGV[1])
        redis.call('HDEL', KEYS[3], ARGV[1])
        return 1
        """;
    private static readonly string RetryScript = CheckTypes + """
        
        checktype(KEYS[2], 'zset')
        checktype(KEYS[3], 'hash')
        if redis.call('HGET', KEYS[3], ARGV[1]) ~= ARGV[2] then return 0 end
        local time = redis.call('TIME')
        local now = time[1] * 1000 + math.floor(time[2] / 1000)
        redis.call('ZADD', KEYS[2], now + 5000, ARGV[1])
        redis.call('HDEL', KEYS[3], ARGV[1])
        return 1
        """;

    public async Task<string?> ReadAsync(Guid id) => await Database.StringGetAsync(options.CartKey(id));
    public async Task<bool> SaveAsync(Guid id, string? expected, string value)
    {
        if (options.ActiveCartTtl <= TimeSpan.Zero) throw new ArgumentException("Cart TTL must be positive.");
        return (int)await Database.ScriptEvaluateAsync(SaveScript, [(RedisKey)options.CartKey(id)],
            [expected is null ? "0" : "1", expected ?? "", value, (long)options.ActiveCartTtl.TotalMilliseconds]) == 1;
    }
    public async Task<bool> CheckoutAsync(Guid id, string expected, string value, Guid eventId, string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return (int)await Database.ScriptEvaluateAsync(CheckoutScript,
            [(RedisKey)options.CartKey(id), options.MessagesKey, options.PendingKey],
            [expected, value, eventId.ToString("N"), message]).WaitAsync(ct) == 1;
    }
    private RedisKey[] OutboxKeys => [(RedisKey)options.MessagesKey, options.PendingKey, options.LeasesKey];
    public async Task<CheckoutLease?> ClaimAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var value = await Database.ScriptEvaluateAsync(ClaimScript, OutboxKeys, [Guid.NewGuid().ToString("N")]).WaitAsync(ct);
        if (value.IsNull) return null;
        var values = (RedisResult[])value!;
        return new(Guid.Parse((string)values[0]!), (string)values[1]!, (string)values[2]!);
    }
    public async Task<bool> AcknowledgeAsync(CheckoutLease lease, CancellationToken ct)
        => (int)await Database.ScriptEvaluateAsync(AckScript, OutboxKeys,
            [lease.EventId.ToString("N"), lease.Token]).WaitAsync(ct) == 1;
    public async Task RetryAsync(CheckoutLease lease, CancellationToken ct)
        => await Database.ScriptEvaluateAsync(RetryScript, OutboxKeys,
            [lease.EventId.ToString("N"), lease.Token]).WaitAsync(ct);
}
