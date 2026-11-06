using Sap.Api.Http;

namespace Sap.UnitTests.Http;

public class BearerTokenTests
{
    [Theory]
    [InlineData("Bearer abc", "abc")]
    [InlineData("bearer abc", "abc")]
    [InlineData("Bearer   abc  ", "abc")]
    public void Token_is_read_from_the_header(string header, string expected)
    {
        Assert.Equal(expected, BearerToken.Read(header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Basic abc")]
    [InlineData("abc")]
    public void Anything_else_means_no_token(string? header)
    {
        Assert.Null(BearerToken.Read(header));
    }
}
