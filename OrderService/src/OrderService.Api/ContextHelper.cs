namespace OrderService.Api;
public static class ContextHelper{
public static Guid GetUserId(this IHttpContextAccessor contextAccessor)
{
    Guid.TryParse( 
        contextAccessor.HttpContext?.User?.FindFirst("sub")
            ?.Value??"00000000-0000-0000-0000-000000000011",out Guid UserId);
    return UserId;
            
}

}