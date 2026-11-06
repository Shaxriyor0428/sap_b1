using MockSl.Application.OData;

namespace MockSl.UnitTests.OData;

public class LiteralConverterTests
{
    private static readonly MockSl.Domain.Metadata.FieldDefinition Quantity =
        MockSl.Domain.Metadata.EntityCatalog.Invoices.Child("DocumentLines").Field("Quantity");

    [Theory]
    [InlineData("9999999999999")]
    [InlineData("-9999999999999")]
    [InlineData("0.5")]
    public void Numbers_the_table_can_hold_are_accepted(string text)
    {
        Assert.Equal(decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture), LiteralConverter.ToDecimal(Quantity, text));
    }

    [Theory]
    [InlineData("10000000000000")]
    [InlineData("79228162514264337593543950335")]
    [InlineData("abc")]
    public void Larger_numbers_are_rejected_before_they_can_overflow(string text)
    {
        Assert.Throws<MockSl.Domain.Errors.ServiceLayerException>(() => LiteralConverter.ToDecimal(Quantity, text));
    }
}

public class SapDateTests
{
    [Theory]
    [InlineData("2014-04-23")]
    [InlineData("20140423")]
    [InlineData("2014-04-23T12:21:21")]
    [InlineData("2014-04-23T12:21:21Z")]
    [InlineData("20140423000000")]
    public void Formats_from_the_guide_are_accepted_and_time_is_ignored(string text)
    {
        Assert.True(SapDate.TryParse(text, out var date));
        Assert.Equal(new DateOnly(2014, 4, 23), date);
    }

    [Theory]
    [InlineData("23.04.2014")]
    [InlineData("2014-13-01")]
    [InlineData("tomorrow")]
    public void Other_text_is_rejected(string text)
    {
        Assert.False(SapDate.TryParse(text, out _));
    }

    [Fact]
    public void Output_uses_the_timestamp_shape()
    {
        Assert.Equal("2026-11-01T00:00:00Z", SapDate.Format(new DateOnly(2026, 11, 1)));
    }
}
