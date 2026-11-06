using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Domain.Metadata;
using MockSl.Infrastructure.Persistence;

namespace MockSl.UnitTests.Persistence;

public class EntitySqlBuilderTests
{
    private static readonly EntityDefinition Entity = EntityCatalog.BusinessPartners;
    private static readonly FieldDefinition CardCode = Entity.FindField("CardCode")!;
    private static readonly FieldDefinition CardName = Entity.FindField("CardName")!;
    private static readonly FieldDefinition Email = Entity.FindField("EmailAddress")!;

    [Fact]
    public void Select_orders_by_key_when_no_order_is_requested()
    {
        var statement = EntitySqlBuilder.Select(Entity, new EntityQuery([CardName, Email], null, [], null, 0));

        Assert.Equal("SELECT \"CardName\", \"E_Mail\" FROM \"OCRD\" ORDER BY \"CardCode\" ASC", statement.Text);
    }

    [Fact]
    public void Select_adds_the_key_as_a_tie_breaker_to_the_requested_order()
    {
        var query = new EntityQuery([CardCode], null, [new OrderByItem(CardName, Descending: true)], null, 0);

        Assert.EndsWith("ORDER BY \"CardName\" DESC, \"CardCode\" ASC", EntitySqlBuilder.Select(Entity, query).Text);
    }

    [Fact]
    public void Select_does_not_repeat_the_key_when_it_is_already_ordered()
    {
        var query = new EntityQuery([CardCode], null, [new OrderByItem(CardCode, Descending: true)], null, 0);

        Assert.EndsWith("ORDER BY \"CardCode\" DESC", EntitySqlBuilder.Select(Entity, query).Text);
    }

    [Fact]
    public void Select_combines_filter_limit_and_offset_with_numbered_parameters()
    {
        var filter = new ComparisonNode(CardName, ComparisonOperator.Equal, "x");
        var statement = EntitySqlBuilder.Select(Entity, new EntityQuery([CardCode], filter, [], 21, 40));

        Assert.Equal(
            "SELECT \"CardCode\" FROM \"OCRD\" WHERE \"CardName\" = @p0 ORDER BY \"CardCode\" ASC LIMIT @p1 OFFSET @p2",
            statement.Text);
        Assert.Equal(["x", 21, 40], statement.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void Insert_returns_the_stored_row()
    {
        var statement = EntitySqlBuilder.Insert(Entity, [new FieldValue(CardCode, "c1"), new FieldValue(Email, "a@b.uz")]);

        Assert.Equal("INSERT INTO \"OCRD\" (\"CardCode\", \"E_Mail\") VALUES (@p0, @p1) RETURNING *", statement.Text);
        Assert.Equal(["c1", "a@b.uz"], statement.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void Update_targets_only_the_given_key()
    {
        var statement = EntitySqlBuilder.Update(Entity, [new FieldValue(CardCode, "c1")], [new FieldValue(CardName, "n")]);

        Assert.Equal("UPDATE \"OCRD\" SET \"CardName\" = @p0 WHERE \"CardCode\" = @p1", statement.Text);
        Assert.Equal(["n", "c1"], statement.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void Two_api_names_of_one_column_read_it_once()
    {
        var invoices = EntityCatalog.Invoices;
        var columns = new[] { invoices.Field("Cancelled"), invoices.Field("CancelStatus") };

        var statement = EntitySqlBuilder.Select(invoices, new EntityQuery(columns, null, [], null, 0));

        Assert.StartsWith("SELECT \"CANCELED\" FROM \"OINV\"", statement.Text);
    }

    [Fact]
    public void Find_for_update_locks_the_row()
    {
        var statement = EntitySqlBuilder.FindForUpdate(Entity, [new FieldValue(CardCode, "c1")]);

        Assert.Equal("SELECT * FROM \"OCRD\" WHERE \"CardCode\" = @p0 FOR NO KEY UPDATE", statement.Text);
    }

    [Fact]
    public void Add_to_number_changes_the_value_in_one_statement()
    {
        var statement = EntitySqlBuilder.AddToNumber(
            Entity,
            [new FieldValue(CardCode, "c1")],
            Entity.Field("CurrentAccountBalance"),
            -25m);

        Assert.Equal("UPDATE \"OCRD\" SET \"Balance\" = \"Balance\" + @p0 WHERE \"CardCode\" = @p1", statement.Text);
        Assert.Equal([-25m, "c1"], statement.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void Delete_targets_only_the_given_key()
    {
        var statement = EntitySqlBuilder.Delete(Entity, [new FieldValue(CardCode, "c1")]);

        Assert.Equal("DELETE FROM \"OCRD\" WHERE \"CardCode\" = @p0", statement.Text);
    }
}
