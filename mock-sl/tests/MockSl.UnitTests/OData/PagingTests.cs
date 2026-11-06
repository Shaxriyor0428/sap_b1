using MockSl.Application.OData;

namespace MockSl.UnitTests.OData;

public class PagingTests
{
    [Theory]
    [InlineData(null, 20, 20)] // default page
    [InlineData(5, 20, 5)] // $top smaller than a page
    [InlineData(50, 20, 20)] // $top larger than a page
    [InlineData(null, 0, null)] // paging off, no limit
    [InlineData(7, 0, 7)] // paging off, $top still applies
    public void Row_limit_respects_both_top_and_page_size(int? top, int pageSize, int? expected)
    {
        Assert.Equal(expected, new PagePlan(top, skip: 0, pageSize).RowLimit);
    }

    [Fact]
    public void One_extra_row_is_fetched_to_detect_a_next_page()
    {
        Assert.Equal(21, new PagePlan(null, 0, 20).FetchLimit);
    }

    [Fact]
    public void Largest_possible_top_does_not_overflow_into_a_negative_limit()
    {
        Assert.Equal(int.MaxValue, new PagePlan(int.MaxValue, 0, pageSize: 0).FetchLimit);
    }

    [Fact]
    public void No_next_page_when_the_extra_row_did_not_arrive()
    {
        Assert.Null(new PagePlan(null, 0, 20).NextPageAfter(20));
    }

    [Fact]
    public void Next_page_moves_skip_forward_by_one_page()
    {
        Assert.Equal(new NextPage(60, null), new PagePlan(null, 40, 20).NextPageAfter(21));
    }

    [Fact]
    public void Top_smaller_than_a_page_has_no_next_page_even_when_more_rows_exist()
    {
        Assert.Null(new PagePlan(5, 0, 20).NextPageAfter(6));
    }

    [Fact]
    public void Top_larger_than_a_page_carries_the_remainder_to_the_next_page()
    {
        Assert.Equal(new NextPage(20, 30), new PagePlan(50, 0, 20).NextPageAfter(21));
    }

    [Fact]
    public void Paging_off_never_has_a_next_page()
    {
        Assert.Null(new PagePlan(null, 0, 0).NextPageAfter(1000));
    }

    [Fact]
    public void Next_link_keeps_the_client_options_and_replaces_top_and_skip()
    {
        var options = new Dictionary<string, string>
        {
            ["$select"] = "CardCode",
            ["$filter"] = "CardType eq 'C'",
            ["$top"] = "50",
            ["$skip"] = "0",
        };

        var link = ODataLinks.BuildNextLink("BusinessPartners", options, new NextPage(20, 30));

        Assert.Equal(
            "BusinessPartners?$select=CardCode&$filter=CardType%20eq%20%27C%27&$top=30&$skip=20",
            link);
    }

    [Theory]
    [InlineData("odata.maxpagesize=50", 50)]
    [InlineData("odata.maxpagesize=0", 0)]
    [InlineData("return-no-content, odata.maxpagesize=5", 5)]
    [InlineData("odata.maxpagesize=abc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Prefer_header_is_read(string? header, int? expected)
    {
        Assert.Equal(expected, PreferHeader.ReadMaxPageSize(header));
    }
}
