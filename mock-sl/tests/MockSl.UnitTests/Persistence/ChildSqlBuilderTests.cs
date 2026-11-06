using MockSl.Application.Entities;
using MockSl.Domain.Metadata;
using MockSl.Infrastructure.Persistence;

namespace MockSl.UnitTests.Persistence;

public class ChildSqlBuilderTests
{
    private static readonly ChildDefinition Lines = EntityCatalog.Invoices.Child("DocumentLines");
    private static readonly ChildDefinition Applied = EntityCatalog.IncomingPayments.Child("PaymentInvoices");

    [Fact]
    public void Select_reads_all_parents_of_a_page_in_one_query()
    {
        var statement = ChildSqlBuilder.Select(Lines, [1, 2, 3]);

        Assert.Equal(
            "SELECT * FROM \"INV1\" WHERE \"DocEntry\" IN (@p0, @p1, @p2) ORDER BY \"DocEntry\" ASC, \"LineNum\" ASC",
            statement.Text);
        Assert.Equal([1, 2, 3], statement.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void Payment_rows_find_their_parent_through_doc_num()
    {
        var statement = ChildSqlBuilder.Select(Applied, [7]);

        Assert.Contains("FROM \"RCT2\" WHERE \"DocNum\" IN (@p0)", statement.Text);
        Assert.EndsWith("\"DocNum\" ASC, \"InvoiceId\" ASC", statement.Text);
    }

    [Fact]
    public void Insert_writes_the_parent_key_first()
    {
        var statement = ChildSqlBuilder.Insert(Lines, 5, [new FieldValue(Lines.Field("ItemCode"), "i1")]);

        Assert.Equal("INSERT INTO \"INV1\" (\"DocEntry\", \"ItemCode\") VALUES (@p0, @p1)", statement.Text);
        Assert.Equal([5, "i1"], statement.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void Update_targets_one_row_of_one_parent()
    {
        var statement = ChildSqlBuilder.Update(Lines, 5, 2, [new FieldValue(Lines.Field("LineStatus"), "C")]);

        Assert.Equal(
            "UPDATE \"INV1\" SET \"LineStatus\" = @p0 WHERE \"DocEntry\" = @p1 AND \"LineNum\" = @p2",
            statement.Text);
        Assert.Equal(["C", 5, 2], statement.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void Update_all_targets_every_row_of_one_parent()
    {
        var statement = ChildSqlBuilder.UpdateAll(Lines, 5, [new FieldValue(Lines.Field("LineStatus"), "C")]);

        Assert.Equal("UPDATE \"INV1\" SET \"LineStatus\" = @p0 WHERE \"DocEntry\" = @p1", statement.Text);
    }
}
