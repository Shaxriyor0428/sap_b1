namespace MockSl.Application.OData;

public sealed record NextPage(int Skip, int? Top);

/// <summary>
/// Works out how many rows to return and whether a next page exists.
/// One extra row is fetched: if it arrives, more data exists beyond this page.
/// </summary>
public sealed class PagePlan
{
    /// <summary>Guide p.35.</summary>
    public const int DefaultPageSize = 20;

    private readonly int? _top;

    /// <param name="top">$top, if given.</param>
    /// <param name="skip">$skip.</param>
    /// <param name="pageSize">Page size. 0 turns paging off (guide p.35).</param>
    public PagePlan(int? top, int skip, int pageSize)
    {
        _top = top;
        Skip = skip;
        RowLimit = pageSize == 0 ? top : Math.Min(pageSize, top ?? pageSize);
    }

    public int Skip { get; }

    /// <summary>The most rows this response may contain. Null means no limit.</summary>
    public int? RowLimit { get; }

    // At int.MaxValue adding one would overflow into a negative LIMIT, and no next page can exist anyway.
    public int? FetchLimit => RowLimit == int.MaxValue ? RowLimit : RowLimit + 1;

    /// <summary>Returns the next page, or null when the client already has everything it asked for.</summary>
    public NextPage? NextPageAfter(int fetchedRows)
    {
        if (RowLimit is not { } limit || fetchedRows <= limit)
        {
            return null;
        }

        if (_top is not { } top)
        {
            return new NextPage(Skip + limit, null);
        }

        // $top caps the total across all pages, so the next link carries what remains of it.
        var remaining = top - limit;
        return remaining > 0 ? new NextPage(Skip + limit, remaining) : null;
    }
}
