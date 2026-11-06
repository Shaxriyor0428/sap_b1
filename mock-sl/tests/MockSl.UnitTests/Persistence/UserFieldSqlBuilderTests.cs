using MockSl.Domain.Metadata;
using MockSl.Infrastructure.Persistence;

namespace MockSl.UnitTests.Persistence;

public class UserFieldSqlBuilderTests
{
    [Theory]
    [InlineData("A", "VARCHAR(50)")]
    [InlineData("M", "TEXT")]
    [InlineData("N", "INT")]
    [InlineData("D", "DATE")]
    [InlineData("B", "NUMERIC(19, 6)")]
    public void Each_type_gets_its_column_type(string type, string columnType)
    {
        var sql = UserFieldSqlBuilder.AddColumn("OCRD", UserFieldPolicy.ToField("Region", type, 50));

        Assert.Equal($"ALTER TABLE \"OCRD\" ADD COLUMN \"U_Region\" {columnType}", sql);
    }

    [Fact]
    public void Resizing_changes_only_the_length()
    {
        Assert.Equal(
            "ALTER TABLE \"OCRD\" ALTER COLUMN \"U_Region\" TYPE VARCHAR(100)",
            UserFieldSqlBuilder.ResizeColumn("OCRD", "U_Region", 100));
    }

    [Fact]
    public void Removing_drops_the_column()
    {
        Assert.Equal("ALTER TABLE \"OCRD\" DROP COLUMN \"U_Region\"", UserFieldSqlBuilder.DropColumn("OCRD", "U_Region"));
    }

    [Fact]
    public void Names_are_quoted_even_though_the_policy_has_checked_them()
    {
        var sql = UserFieldSqlBuilder.DropColumn("OCRD", "x\"; DROP TABLE \"OCRD");

        Assert.Equal("ALTER TABLE \"OCRD\" DROP COLUMN \"x\"\"; DROP TABLE \"\"OCRD\"", sql);
    }

    [Fact]
    public void Linked_objects_are_read_with_one_query_and_parameters()
    {
        var entity = EntityCatalog.BusinessPartners;
        var columns = new[] { entity.Field("CardName"), entity.Field("CardCode") };

        var statement = EntitySqlBuilder.FindMany(entity, ["C1", "C2"], columns);

        Assert.Equal("SELECT \"CardName\", \"CardCode\" FROM \"OCRD\" WHERE \"CardCode\" IN (@p0, @p1)", statement.Text);
        Assert.Equal(["C1", "C2"], statement.Parameters.Select(parameter => parameter.Value));
    }
}
