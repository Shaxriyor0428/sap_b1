using Sap.Infrastructure.ServiceLayer;

namespace Sap.UnitTests.ServiceLayer;

public class ServiceLayerErrorParserTests
{
    [Theory]
    [InlineData("""{"error":{"code":"-2039","message":"Modified"}}""", "-2039", "Modified")] // v2, guide p.162
    [InlineData("""{"error":{"code":-10,"message":{"lang":"en-us","value":"Exists"}}}""", "-10", "Exists")] // v1, guide p.25
    [InlineData("""{"error":{"code":"-1017","details":[{"code":"","message":""}],"message":"Bad"}}""", "-1017", "Bad")] // extra field
    public void Both_known_error_shapes_are_read(string body, string expectedCode, string expectedMessage)
    {
        var error = ServiceLayerErrorParser.Parse(400, body);

        Assert.Equal(400, error.StatusCode);
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedMessage, error.Message);
    }

    [Theory]
    [InlineData("<html>Bad Gateway</html>")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("""{"message":"no error object"}""")]
    [InlineData("""{"error":"just text"}""")]
    [InlineData("""{"error":{}}""")]
    [InlineData("""{"error":{"code":{},"message":123}}""")]
    public void Anything_else_still_reports_the_status(string body)
    {
        var error = ServiceLayerErrorParser.Parse(502, body);

        Assert.Equal(502, error.StatusCode);
        Assert.Equal("UNKNOWN", error.Code);
        Assert.Equal("The Service Layer returned HTTP 502.", error.Message);
    }
}
