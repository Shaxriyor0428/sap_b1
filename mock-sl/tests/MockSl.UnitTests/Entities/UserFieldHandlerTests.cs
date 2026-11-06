using System.Text.Json;
using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Application.Rules;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;
using MockSl.UnitTests.Fakes;

namespace MockSl.UnitTests.Entities;

/// <summary>The steps of user-defined fields and $expand, from the request body down to the database calls.</summary>
public class UserFieldHandlerTests
{
    private static readonly EntityDefinition Metadata = EntityCatalog.UserFieldsMD;

    private static readonly IReadOnlyList<FieldValue> Key =
        [new FieldValue(Metadata.Field("TableName"), "OCRD"), new FieldValue(Metadata.Field("FieldID"), 0)];

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private static EntityHandler NewHandler(FakeEntityStore store) => new(store, new RulesCatalog(TimeProvider.System));

    private static EntityRow FieldRow(int id = 0, string type = "A", int editSize = 50) => new(new Dictionary<string, object?>
    {
        ["TableID"] = "OCRD",
        ["FieldID"] = id,
        ["AliasID"] = "Region",
        ["Descr"] = "Hudud",
        ["TypeID"] = type,
        ["EditType"] = string.Empty,
        ["SizeID"] = editSize,
        ["EditSize"] = editSize,
        ["Dflt"] = null,
        ["NotNull"] = "N",
        ["RTable"] = null,
        ["RelUDO"] = null,
    });

    private const string RegionBody = """
        {"Name":"Region","Type":"db_Alpha","Size":50,"Description":"Hudud","SubType":"st_None","TableName":"OCRD"}
        """;

    private static object? Inserted(FakeEntityStore store, string column) =>
        store.InsertedValues.First(value => value.Field.Column == column).Value;

    [Fact]
    public async Task Creating_adds_the_column_and_saves_the_description()
    {
        var store = new FakeEntityStore { InsertedRow = FieldRow() };

        var created = await NewHandler(store).CreateAsync(Metadata, Json(RegionBody), default);

        Assert.Contains("add column OCRD.U_Region", store.Steps);
        Assert.Contains("insert", store.Steps);
        Assert.True(store.Committed);
        Assert.Equal("(TableName='OCRD',FieldID=0)", created.KeyInUrl);
    }

    [Fact]
    public async Task The_answer_has_the_shape_of_the_guide()
    {
        var store = new FakeEntityStore { InsertedRow = FieldRow() };

        var created = await NewHandler(store).CreateAsync(Metadata, Json(RegionBody), default);

        Assert.Equal("Region", created.Value["Name"]!.GetValue<string>());
        Assert.Equal("db_Alpha", created.Value["Type"]!.GetValue<string>());
        Assert.Equal("st_None", created.Value["SubType"]!.GetValue<string>());
        Assert.Equal("tNO", created.Value["Mandatory"]!.GetValue<string>());
        Assert.Equal(50, created.Value["EditSize"]!.GetValue<int>());
        Assert.Null(created.Value["LinkedTable"]);
    }

    [Fact]
    public async Task The_column_comes_first_so_a_parallel_request_cannot_take_the_same_id()
    {
        var store = new FakeEntityStore { InsertedRow = FieldRow() };

        await NewHandler(store).CreateAsync(Metadata, Json(RegionBody), default);

        Assert.Equal(["add column OCRD.U_Region", "query", "insert"], store.Steps);
    }

    [Fact]
    public async Task The_id_follows_the_fields_of_the_table()
    {
        var store = new FakeEntityStore { InsertedRow = FieldRow(), Rows = [FieldRow(id: 0), FieldRow(id: 1)] };

        await NewHandler(store).CreateAsync(Metadata, Json(RegionBody), default);

        Assert.Equal(2, Inserted(store, "FieldID"));
    }

    [Fact]
    public async Task Size_alone_fills_both_size_columns()
    {
        var store = new FakeEntityStore { InsertedRow = FieldRow() };

        await NewHandler(store).CreateAsync(Metadata, Json(RegionBody), default);

        Assert.Equal(50, Inserted(store, "SizeID"));
        Assert.Equal(50, Inserted(store, "EditSize"));
    }

    [Fact]
    public async Task A_missing_sub_type_is_stored_as_none()
    {
        var store = new FakeEntityStore { InsertedRow = FieldRow() };
        var body = """{"Name":"Region","Type":"db_Alpha","Size":50,"TableName":"OCRD"}""";

        await NewHandler(store).CreateAsync(Metadata, Json(body), default);

        Assert.Equal(string.Empty, Inserted(store, "EditType"));
    }

    [Theory]
    [InlineData("""{"Name":"a; DROP TABLE x","Type":"db_Alpha","Size":10,"TableName":"OCRD"}""")]
    [InlineData("""{"Name":"Region","Type":"db_Alpha","Size":10,"TableName":"OUSR"}""")]
    [InlineData("""{"Name":"Region","Type":"db_Alpha","Size":10,"TableName":"OCRD\"; DROP TABLE \"OCRD"}""")]
    [InlineData("""{"Name":"Region","Type":"db_Alpha","TableName":"OCRD"}""")]
    [InlineData("""{"Name":"Region","Type":"db_Alpha","Size":255,"TableName":"OCRD"}""")]
    [InlineData("""{"Name":"Region","Type":"db_Nope","Size":10,"TableName":"OCRD"}""")]
    [InlineData("""{"Type":"db_Alpha","Size":10,"TableName":"OCRD"}""")]
    [InlineData("""{"Name":"Region","Size":10,"TableName":"OCRD"}""")]
    [InlineData("""{"Name":"Region","Type":"db_Alpha","Size":10}""")]
    [InlineData("""{"Name":null,"Type":"db_Alpha","Size":10,"TableName":"OCRD"}""")]
    public async Task A_bad_request_changes_nothing_in_the_database(string body)
    {
        var store = new FakeEntityStore { InsertedRow = FieldRow() };

        await Assert.ThrowsAsync<ServiceLayerException>(() => NewHandler(store).CreateAsync(Metadata, Json(body), default));

        Assert.Empty(store.Steps);
        Assert.False(store.Committed);
    }

    [Fact]
    public async Task Growing_a_text_field_changes_the_column_and_both_sizes()
    {
        var store = new FakeEntityStore { Rows = [FieldRow(editSize: 50)] };

        await NewHandler(store).UpdateAsync(Metadata, Key, Json("""{"EditSize":100,"Description":"Yangi"}"""), default);

        Assert.Equal(["resize column OCRD.U_Region 100"], store.Steps);
        Assert.True(store.UpdateCalled);
        Assert.True(store.Committed);
    }

    [Fact]
    public async Task Changing_only_the_description_does_not_touch_the_column()
    {
        var store = new FakeEntityStore { Rows = [FieldRow()] };

        await NewHandler(store).UpdateAsync(Metadata, Key, Json("""{"Description":"Yangi"}"""), default);

        Assert.Empty(store.Steps);
        Assert.True(store.UpdateCalled);
    }

    [Fact]
    public async Task The_type_cannot_be_changed_as_the_guide_says()
    {
        var store = new FakeEntityStore { Rows = [FieldRow()] };

        await NewHandler(store).UpdateAsync(Metadata, Key, Json("""{"Type":"db_Memo"}"""), default);

        Assert.False(store.UpdateCalled);
        Assert.Empty(store.Steps);
    }

    [Fact]
    public async Task Shrinking_is_refused_because_stored_values_might_not_fit()
    {
        var store = new FakeEntityStore { Rows = [FieldRow(editSize: 50)] };

        await Assert.ThrowsAsync<ServiceLayerException>(() =>
            NewHandler(store).UpdateAsync(Metadata, Key, Json("""{"EditSize":10}"""), default));

        Assert.Empty(store.Steps);
        Assert.False(store.Committed);
    }

    [Fact]
    public async Task Resizing_a_field_that_does_not_exist_returns_not_found()
    {
        var store = new FakeEntityStore();

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            NewHandler(store).UpdateAsync(Metadata, Key, Json("""{"EditSize":100}"""), default));

        Assert.Equal("-2028", error.Code);
    }

    [Fact]
    public async Task Other_types_have_no_column_length_to_change()
    {
        var store = new FakeEntityStore { Rows = [FieldRow(type: "N", editSize: 10)] };

        await NewHandler(store).UpdateAsync(Metadata, Key, Json("""{"EditSize":20}"""), default);

        Assert.Empty(store.Steps);
        Assert.True(store.UpdateCalled);
    }

    [Fact]
    public async Task Deleting_drops_the_column()
    {
        var store = new FakeEntityStore { Rows = [FieldRow()] };

        await NewHandler(store).DeleteAsync(Metadata, Key, default);

        Assert.Equal(["drop column OCRD.U_Region"], store.Steps);
        Assert.True(store.Committed);
    }

    [Fact]
    public async Task Deleting_a_field_that_does_not_exist_drops_nothing()
    {
        var store = new FakeEntityStore();

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() => NewHandler(store).DeleteAsync(Metadata, Key, default));

        Assert.Equal("-2028", error.Code);
        Assert.Empty(store.Steps);
    }

    [Fact]
    public async Task A_new_field_appears_on_its_object()
    {
        var store = new FakeEntityStore { Rows = [FieldRow()] };

        var partners = await NewHandler(store).WithUserFieldsAsync(EntityCatalog.BusinessPartners, default);

        Assert.Equal(50, partners.Field("U_Region").MaxLength);
    }

    [Fact]
    public void Rules_still_apply_to_a_copy_with_user_fields()
    {
        var rules = new RulesCatalog(TimeProvider.System);
        var copy = EntityCatalog.Invoices.WithUserFields(Enumerable.Empty<(string, FieldDefinition)>().ToLookup(pair => pair.Item1, pair => pair.Item2));

        Assert.IsType<DocumentRules>(rules.For(copy));
    }

    private static EntityRow InvoiceRow(int entry, string? cardCode) => new(new Dictionary<string, object?>
    {
        ["DocEntry"] = entry,
        ["DocNum"] = entry + 1000,
        ["DocTotal"] = 100m,
        ["CardCode"] = cardCode,
    });

    private static EntityRow PartnerRow(string code, string name) => new(new Dictionary<string, object?>
    {
        ["CardCode"] = code,
        ["CardName"] = name,
        ["Phone1"] = "+998",
    });

    private static ODataQuery ExpandQuery() => ODataQuery.Parse(EntityCatalog.Invoices, new Dictionary<string, string>
    {
        ["$select"] = "DocEntry,DocNum,DocTotal",
        ["$expand"] = "BusinessPartner($select=CardName,Phone1)",
    });

    [Fact]
    public async Task A_page_reads_its_linked_objects_with_one_query()
    {
        var store = new FakeEntityStore
        {
            Rows = [InvoiceRow(1, "C1"), InvoiceRow(2, "C2"), InvoiceRow(3, "C1")],
            LinkedRows = [PartnerRow("C1", "Ali"), PartnerRow("C2", "Vali")],
        };

        var page = await NewHandler(store).ListAsync(EntityCatalog.Invoices, ExpandQuery(), new PagePlan(null, 0, 20), default);

        Assert.Single(store.Steps, step => step == "find many");
        Assert.Equal(["C1", "C2"], store.LastLinkedKeys);
        Assert.Equal("Ali", page.Value[0]!["BusinessPartner"]!["CardName"]!.GetValue<string>());
        Assert.Equal("Vali", page.Value[1]!["BusinessPartner"]!["CardName"]!.GetValue<string>());
        Assert.Equal("Ali", page.Value[2]!["BusinessPartner"]!["CardName"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_answer_has_only_the_fields_that_were_asked_for()
    {
        var store = new FakeEntityStore { Rows = [InvoiceRow(1, "C1")], LinkedRows = [PartnerRow("C1", "Ali")] };

        var invoice = await NewHandler(store).GetAsync(EntityCatalog.Invoices, EntityKey.Of(EntityCatalog.Invoices, 1), ExpandQuery(), default);

        Assert.Equal(["DocEntry", "DocNum", "DocTotal", "BusinessPartner"], invoice.Select(property => property.Key));
        Assert.Equal(["CardName", "Phone1"], invoice["BusinessPartner"]!.AsObject().Select(property => property.Key));
    }

    [Fact]
    public async Task The_foreign_key_is_read_even_when_it_was_not_selected()
    {
        var store = new FakeEntityStore { Rows = [InvoiceRow(1, "C1")], LinkedRows = [PartnerRow("C1", "Ali")] };

        await NewHandler(store).ListAsync(EntityCatalog.Invoices, ExpandQuery(), new PagePlan(null, 0, 20), default);

        Assert.Contains(store.LastQuery!.Columns, column => column.Name == "CardCode");
    }

    [Fact]
    public async Task A_document_without_a_partner_shows_null()
    {
        var store = new FakeEntityStore { Rows = [InvoiceRow(1, null)] };

        var page = await NewHandler(store).ListAsync(EntityCatalog.Invoices, ExpandQuery(), new PagePlan(null, 0, 20), default);

        Assert.Null(page.Value[0]!["BusinessPartner"]);
        Assert.Empty(store.LastLinkedKeys);
    }

    [Fact]
    public async Task Selecting_only_the_link_still_builds_a_valid_column_list()
    {
        var query = ODataQuery.Parse(EntityCatalog.Invoices, new Dictionary<string, string> { ["$select"] = "BusinessPartner" });
        var store = new FakeEntityStore { Rows = [InvoiceRow(1, "C1")] };

        var page = await NewHandler(store).ListAsync(EntityCatalog.Invoices, query, new PagePlan(null, 0, 20), default);

        Assert.NotEmpty(store.LastQuery!.Columns);
        Assert.Empty(page.Value[0]!.AsObject());
    }
}
