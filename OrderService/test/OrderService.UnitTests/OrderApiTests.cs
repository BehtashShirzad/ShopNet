using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OrderService.Api;

namespace OrderService.UnitTests;

public class OrderApiTests
{
    [Fact]
    public void ContextHelper_ReadsSubjectClaim()
    {
        var userId = Guid.NewGuid();
        IHttpContextAccessor accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("sub", userId.ToString())
                ]))
            }
        };

        Assert.Equal(userId, accessor.GetUserId());
    }

    [Fact]
    public void ContextHelper_UsesFallbackWithoutSubject()
    {
        IHttpContextAccessor accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000011"), accessor.GetUserId());
    }
}
