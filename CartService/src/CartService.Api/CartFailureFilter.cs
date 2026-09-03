using CartService.Application.Checkout;
using Grpc.Core;
using StackExchange.Redis;

namespace CartService.Api;

public sealed class CartFailureFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (CheckoutRejectedException exception)
        {
            return Results.Problem(statusCode: exception.Code is "cart_not_found" or "product_not_found" ? 404 : 409,
                title: exception.Code, detail: exception.Message);
        }
        catch (CartConcurrencyException exception)
        {
            return Results.Problem(statusCode: 409, title: "cart_changed", detail: exception.Message);
        }
        catch (RpcException) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "dependency_unavailable",
                detail: "Product/availability verification failed. Checkout was not accepted.");
        }
        catch (RedisException)
        {
            // A network timeout can be ambiguous; reloading/retrying the same CartId is idempotent.
            return Results.Problem(statusCode: 503, title: "cart_storage_unavailable",
                detail: "Reload the cart and retry using the same CartId.");
        }
    }
}
