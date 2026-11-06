using System.Text.Json;
using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Application.Rules;
using MockSl.Application.SqlQueries;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;
using MockSl.Domain.SqlQueries;
using MockSl.Infrastructure.Persistence;
using MockSl.UnitTests.Fakes;

namespace MockSl.UnitTests.SqlQueries;

/// <summary>Saving and running a query, from the request down to the database calls.</summary>
public class SqlQueryHandlingTests
{
    private static readonly EntityDefinition Definition = EntityCatalog.SqlQueries;
    private static readonly IReadOnlyList<FieldValue> Key = [new FieldValue(Definition.Field("SqlCode"), "q1")];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Catalog = new Dictionary<string, IReadOnlyList<string>>
    {
        ["OPCH"] = ["DocEntry", "DocNum", "DocDate"],
    };

    private const string RawText = "select docnum from opch where docdate >= :fromDate";
    private const string StoredText = "select \"DocNum\" from \"OPCH\" where \"DocDate\" >= :fromDate";

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private static EntityHandler NewHandler(FakeEntityStore store) => new(store, new RulesCatalog(TimeProvider.System));

    private static EntityRow SavedRow(string text = StoredText, string? paramList = "fromDate") => new(new Dictionary<string, object?>
    {
        ["SqlCode"] = "q1",
        ["SqlName"] = "Xaridlar",
        ["SqlText"] = text,
        ["ParamList"] = paramList,
        ["CreateDate"] = new DateTime(2026, 11, 7),
        ["UpdateDate"] = new DateTime(2026, 11, 7),
    });

    private static SqlRow Row(int docNum) => new([new SqlCell("DocNum", docNum)]);

    private static object? Inserted(FakeEntityStore store, string column) =>
        store.InsertedValues.First(value => value.Field.Column == column).Value;

    [Fact]
    public async Task Saving_stores_the_normalized_text_and_the_parameter_names()
    {
        var store = new FakeEntityStore { TableColumns = Catalog, InsertedRow = SavedRow() };

        var created = await NewHandler(store).CreateAsync(Definition, Json($$"""{"SqlCode":"q1","SqlText":"{{RawText}}"}"""), default);

        Assert.Equal(StoredText, Inserted(store, "SqlText"));
        Assert.Equal("fromDate", Inserted(store, "ParamList"));
        Assert.IsType<DateOnly>(Inserted(store, "CreateDate"));
        Assert.Equal("('q1')", created.KeyInUrl);
        Assert.True(store.Committed);
    }

    [Fact]
    public async Task A_client_cannot_set_the_parameter_list_or_the_dates()
    {
        var store = new FakeEntityStore { TableColumns = Catalog, InsertedRow = SavedRow() };
        var body = """{"SqlCode":"q1","SqlText":"select docnum from opch","ParamList":"x","CreateDate":"2000-01-01"}""";

        await NewHandler(store).CreateAsync(Definition, Json(body), default);

        Assert.Null(Inserted(store, "ParamList"));
        Assert.NotEqual(new DateOnly(2000, 1, 1), Inserted(store, "CreateDate"));
    }

    [Theory]
    [InlineData("""{"SqlCode":"q1","SqlText":"update OPCH set DocNum = 1"}""", "701")]
    [InlineData("""{"SqlCode":"q1","SqlText":"select usename from pg_user"}""", "702")]
    [InlineData("""{"SqlCode":"q1","SqlText":"select Nope from OPCH"}""", "703")]
    [InlineData("""{"SqlCode":"q1"}""", "-10")]
    [InlineData("""{"SqlText":"select DocNum from OPCH"}""", "-10")]
    public async Task Bad_sql_is_refused_when_it_is_saved(string body, string code)
    {
        var store = new FakeEntityStore { TableColumns = Catalog, InsertedRow = SavedRow() };

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() => NewHandler(store).CreateAsync(Definition, Json(body), default));

        Assert.Equal(code, error.Code);
        Assert.DoesNotContain("insert", store.Steps);
        Assert.False(store.Committed);
    }

    [Fact]
    public async Task Changing_the_text_checks_it_again()
    {
        var store = new FakeEntityStore { TableColumns = Catalog };

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            NewHandler(store).UpdateAsync(Definition, Key, Json("""{"SqlText":"delete from OPCH"}"""), default));

        Assert.Equal("701", error.Code);
        Assert.False(store.UpdateCalled);
    }

    [Fact]
    public async Task Changing_only_the_name_does_not_need_the_catalog()
    {
        var store = new FakeEntityStore();

        await NewHandler(store).UpdateAsync(Definition, Key, Json("""{"SqlName":"Yangi"}"""), default);

        Assert.True(store.UpdateCalled);
    }

    [Fact]
    public async Task Running_sends_the_value_apart_from_the_text()
    {
        var store = new FakeEntityStore { TableColumns = Catalog, Rows = [SavedRow()], SqlRows = [Row(2001)] };

        var result = await new SqlQueryRunner(store).ListAsync(
            Key,
            ParamListParser.Parse("fromDate='2026-10-01'"),
            new PagePlan(null, 0, 20),
            default);

        Assert.Equal("select \"DocNum\" from \"OPCH\" where \"DocDate\" >= @fromDate", store.LastRun!.Executable);
        Assert.Equal("2026-10-01", store.LastRun.Parameters["fromDate"]);
        Assert.Equal(StoredText, result.SqlText);
        Assert.Equal(2001, result.Value[0]!["DocNum"]!.GetValue<int>());
        Assert.Null(result.NextLink);
    }

    [Theory]
    [InlineData("")]
    [InlineData("other=1")]
    [InlineData("fromDate='2026-10-01'&extra=1")]
    [InlineData("fromDate='2026-10-01'&fromDate='2026-10-02'")]
    public async Task Parameters_must_match_the_query_exactly(string paramList)
    {
        var store = new FakeEntityStore { TableColumns = Catalog, Rows = [SavedRow()] };

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            new SqlQueryRunner(store).ListAsync(Key, ParamListParser.Parse(paramList), new PagePlan(null, 0, 20), default));

        Assert.Equal("704", error.Code);
        Assert.Null(store.LastRun);
    }

    [Fact]
    public async Task Running_a_query_that_does_not_exist_returns_not_found()
    {
        var store = new FakeEntityStore { TableColumns = Catalog };

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            new SqlQueryRunner(store).ListAsync(Key, [], new PagePlan(null, 0, 20), default));

        Assert.Equal("-2028", error.Code);
    }

    [Fact]
    public async Task A_query_whose_column_is_gone_is_refused_when_it_runs()
    {
        var store = new FakeEntityStore { TableColumns = Catalog, Rows = [SavedRow("select \"U_Gone\" from \"OPCH\"", null)] };

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            new SqlQueryRunner(store).ListAsync(Key, [], new PagePlan(null, 0, 20), default));

        Assert.Equal("703", error.Code);
    }

    [Fact]
    public async Task One_row_more_than_a_page_gives_the_next_link_of_the_guide()
    {
        var store = new FakeEntityStore { TableColumns = Catalog, Rows = [SavedRow()], SqlRows = [Row(1), Row(2), Row(3)] };

        var result = await new SqlQueryRunner(store).ListAsync(
            Key,
            ParamListParser.Parse("fromDate='2026-10-01'"),
            new PagePlan(null, 4, 2),
            default);

        Assert.Equal(3, store.LastRun!.Limit);
        Assert.Equal(4, store.LastRun.Offset);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("SQLQueries('q1')/List?fromDate=%272026-10-01%27&$skip=6", result.NextLink);
    }

    [Fact]
    public async Task Values_keep_their_types_and_dates_have_the_format_of_the_guide()
    {
        var row = new SqlRow(
        [
            new SqlCell("Text", "x"),
            new SqlCell("Count", 5L),
            new SqlCell("Total", 12.5m),
            new SqlCell("Date", new DateTime(2020, 10, 7)),
            new SqlCell("Empty", null),
        ]);
        var store = new FakeEntityStore { TableColumns = Catalog, Rows = [SavedRow("select \"DocNum\" from \"OPCH\"", null)], SqlRows = [row] };

        var result = await new SqlQueryRunner(store).ListAsync(Key, [], new PagePlan(null, 0, 20), default);

        Assert.Equal(
            """{"Text":"x","Count":5,"Total":12.5,"Date":"20201007","Empty":null}""",
            result.Value[0]!.ToJsonString());
    }

    [Fact]
    public void Paging_wraps_the_query_as_in_the_guide()
    {
        Assert.Equal(
            "SELECT * FROM (select 1) B1S_DUMMY_TABLE LIMIT 21 OFFSET 40",
            SqlPaging.Wrap("select 1", 21, 40));
    }

    [Fact]
    public void Without_a_page_size_there_is_no_limit()
    {
        Assert.Equal("SELECT * FROM (select 1) B1S_DUMMY_TABLE OFFSET 0", SqlPaging.Wrap("select 1", null, 0));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void A_superuser_may_do_everything_and_anyone_may_read(bool isSuperUser, bool isPlainRead)
    {
        SqlQueryPolicy.EnsurePermitted(isSuperUser, isPlainRead);
    }

    [Fact]
    public void A_normal_user_gets_the_error_of_the_guide()
    {
        var error = Assert.Throws<ServiceLayerException>(() => SqlQueryPolicy.EnsurePermitted(isSuperUser: false, isPlainRead: false));

        Assert.Equal(ServiceLayerErrorKind.Forbidden, error.Kind);
        Assert.Equal("-6006", error.Code);
        Assert.Equal("You are not permitted to perform this action", error.Message);
    }

    [Theory]
    [InlineData("OHEM")]
    [InlineData("OACT")]
    [InlineData("OUSR")]
    [InlineData("OSQL")]
    [InlineData("CUFD")]
    public void Tables_outside_the_list_of_the_guide_stay_out(string table)
    {
        Assert.DoesNotContain(table, SqlQueryPolicy.AllowedTables);
    }
}
