using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.Metadata;

public class UserFieldPolicyTests
{
    [Theory]
    [InlineData("Region")]
    [InlineData("u1")]
    [InlineData("A_b_9")]
    public void Plain_names_are_accepted(string name)
    {
        UserFieldPolicy.EnsureValidName(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1abc")]
    [InlineData("_abc")]
    [InlineData("a b")]
    [InlineData("a-b")]
    [InlineData("a;b")]
    [InlineData("a'b")]
    [InlineData("a\"b")]
    [InlineData("a\nb")]
    [InlineData("hududç")]
    [InlineData("x\" TEXT; DROP TABLE \"OCRD\"; --")]
    public void Names_that_are_not_plain_are_refused(string name)
    {
        var error = Assert.Throws<ServiceLayerException>(() => UserFieldPolicy.EnsureValidName(name));

        Assert.Equal("-10", error.Code);
    }

    [Theory]
    [InlineData("OCRD")]
    [InlineData("OHEM")]
    [InlineData("OITM")]
    [InlineData("OACT")]
    [InlineData("OINV")]
    [InlineData("OPCH")]
    [InlineData("ORCT")]
    [InlineData("OVPM")]
    public void The_tables_of_business_objects_are_allowed(string table)
    {
        UserFieldPolicy.EnsureAllowedTable(table);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ocrd")]
    [InlineData("OUSR")]
    [InlineData("CUFD")]
    [InlineData("INV1")]
    [InlineData("pg_user")]
    [InlineData("OCRD\"; DROP TABLE \"OCRD")]
    public void Every_other_table_is_refused(string table)
    {
        var error = Assert.Throws<ServiceLayerException>(() => UserFieldPolicy.EnsureAllowedTable(table));

        Assert.Equal("-10", error.Code);
    }

    [Fact]
    public void The_first_field_of_a_table_has_id_zero_as_in_the_guide()
    {
        Assert.Equal(0, UserFieldPolicy.NextFieldId([]));
    }

    [Fact]
    public void The_next_id_follows_the_largest_one()
    {
        Assert.Equal(3, UserFieldPolicy.NextFieldId([0, 2]));
    }

    [Theory]
    [InlineData(20, null, 20)]
    [InlineData(null, 10, 10)]
    [InlineData(20, 10, 20)]
    [InlineData(1, null, 1)]
    [InlineData(254, null, 254)]
    public void A_text_field_takes_its_size_from_edit_size_or_size(int? editSize, int? size, int expected)
    {
        Assert.Equal(expected, UserFieldPolicy.ResolveEditSize("A", editSize, size));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(255, null)]
    [InlineData(null, 255)]
    public void A_text_field_needs_a_size_that_fits(int? editSize, int? size)
    {
        var error = Assert.Throws<ServiceLayerException>(() => UserFieldPolicy.ResolveEditSize("A", editSize, size));

        Assert.Equal("-10", error.Code);
    }

    [Theory]
    [InlineData("M")]
    [InlineData("N")]
    [InlineData("D")]
    [InlineData("B")]
    public void Other_types_do_not_need_a_size(string type)
    {
        Assert.Equal(0, UserFieldPolicy.ResolveEditSize(type, null, null));
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 11)]
    [InlineData(10, 254)]
    public void A_text_field_can_grow(int current, int requested)
    {
        UserFieldPolicy.EnsureCanResize("A", current, requested);
    }

    [Theory]
    [InlineData(10, 9)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    [InlineData(10, 255)]
    public void A_text_field_cannot_shrink_or_outgrow_the_limit(int current, int requested)
    {
        Assert.Throws<ServiceLayerException>(() => UserFieldPolicy.EnsureCanResize("A", current, requested));
    }

    [Fact]
    public void The_size_of_other_types_is_not_limited_because_no_column_depends_on_it()
    {
        UserFieldPolicy.EnsureCanResize("N", 10, 5);
    }

    [Fact]
    public void The_field_is_named_with_the_prefix_in_the_api_and_in_the_database()
    {
        var field = UserFieldPolicy.ToField("Region", "A", 50);

        Assert.Equal("U_Region", field.Name);
        Assert.Equal("U_Region", field.Column);
    }

    [Theory]
    [InlineData("A", FieldType.Text, 50)]
    [InlineData("M", FieldType.Text, null)]
    [InlineData("N", FieldType.Integer, null)]
    [InlineData("D", FieldType.Date, null)]
    [InlineData("B", FieldType.Decimal, null)]
    public void Each_type_maps_to_a_field_type(string type, FieldType expected, int? maxLength)
    {
        var field = UserFieldPolicy.ToField("Region", type, 50);

        Assert.Equal(expected, field.Type);
        Assert.Equal(maxLength, field.MaxLength);
    }

    [Theory]
    [InlineData("db_Alpha", "A")]
    [InlineData("db_Memo", "M")]
    [InlineData("db_Numeric", "N")]
    [InlineData("db_Date", "D")]
    [InlineData("db_Float", "B")]
    public void Each_type_name_has_one_stored_letter(string name, string letter)
    {
        Assert.Equal(name, SapEnums.BoFieldTypes.GetName(letter));
    }

    [Fact]
    public void A_copy_with_user_fields_changes_only_the_objects_of_that_table()
    {
        var fields = new[] { ("OCRD", UserFieldPolicy.ToField("Region", "A", 50)) }
            .ToLookup(pair => pair.Item1, pair => pair.Item2);

        var partners = EntityCatalog.BusinessPartners.WithUserFields(fields);
        var items = EntityCatalog.Items.WithUserFields(fields);

        Assert.NotNull(partners.FindField("U_Region"));
        Assert.Null(items.FindField("U_Region"));
        Assert.Null(EntityCatalog.BusinessPartners.FindField("U_Region"));
    }

    [Fact]
    public void A_copy_with_user_fields_keeps_everything_else()
    {
        var copy = EntityCatalog.Invoices.WithUserFields(Enumerable.Empty<(string, FieldDefinition)>().ToLookup(pair => pair.Item1, pair => pair.Item2));

        Assert.Equal(EntityCatalog.Invoices.SetName, copy.SetName);
        Assert.Equal(EntityCatalog.Invoices.Key, copy.Key);
        Assert.Equal(EntityCatalog.Invoices.Children, copy.Children);
        Assert.False(copy.CanDelete);
    }

    [Fact]
    public void A_linked_object_shows_its_user_fields_too()
    {
        var fields = new[] { ("OCRD", UserFieldPolicy.ToField("Region", "A", 50)) }
            .ToLookup(pair => pair.Item1, pair => pair.Item2);

        var invoices = EntityCatalog.Invoices.WithUserFields(fields);

        Assert.NotNull(invoices.FindNavigation("BusinessPartner")!.Target.FindField("U_Region"));
    }
}
