namespace CartService.Infrastructure;

public sealed class GrpcCallOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    public void Validate()
    {
        if (Timeout < TimeSpan.FromSeconds(1) || Timeout > TimeSpan.FromSeconds(30))
            throw new ArgumentException("gRPC timeout must be between 1 and 30 seconds.");
    }
}
