using IdentityService.Dtos;

namespace IdentityService.UnitTests;

public class AuthResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        var result = AuthResult.Ok("user-id");

        Assert.True(result.Success);
        Assert.Equal("user-id", result.UserId);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_CreatesFailedResult()
    {
        var result = AuthResult.Fail("failure");

        Assert.False(result.Success);
        Assert.Null(result.UserId);
        Assert.Equal("failure", result.Error);
    }
}
