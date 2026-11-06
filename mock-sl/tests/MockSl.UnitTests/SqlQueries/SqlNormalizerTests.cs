using MockSl.Application.SqlQueries;
using MockSl.Domain.Errors;

namespace MockSl.UnitTests.SqlQueries;

public class SqlNormalizerTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Catalog = new Dictionary<string, IReadOnlyList<string>>
    {
        ["OITM"] = ["ItemCode", "ItemName", "OnHand"],
        ["OINV"] = ["DocEntry", "DocNum", "DocStatus", "DocType", "DocDate", "CardCode", "DocTotal", "Comments", "CANCELED"],
        ["INV1"] = ["DocEntry", "LineNum", "ItemCode", "Quantity", "Price"],
        ["OPCH"] = ["DocEntry", "DocNum", "DocDate", "CardCode"],
        ["PCH1"] = ["DocEntry", "ItemCode", "Quantity", "Price"],
        ["OCRD"] = ["CardCode", "CardName", "U_Region"],
        ["ORCT"] = ["DocEntry", "Canceled"],
    };

    private static NormalizedSql Normalize(string sql) => SqlNormalizer.Normalize(sql, Catalog);

    private static ServiceLayerException Refused(string sql) =>
        Assert.Throws<ServiceLayerException>(() => Normalize(sql));

    [Fact]
    public void The_query_of_the_task_gets_exact_names_and_keeps_its_parameter()
    {
        var result = Normalize("SELECT T0.DocNum, T0.DocDate, T0.CardCode, T1.ItemCode, T1.Quantity, T1.Price FROM OPCH T0 INNER JOIN PCH1 T1 ON T0.DocEntry = T1.DocEntry WHERE T0.DocDate >= :fromDate");

        Assert.Equal(
            "SELECT T0.\"DocNum\", T0.\"DocDate\", T0.\"CardCode\", T1.\"ItemCode\", T1.\"Quantity\", T1.\"Price\" FROM \"OPCH\" T0 INNER JOIN \"PCH1\" T1 ON T0.\"DocEntry\" = T1.\"DocEntry\" WHERE T0.\"DocDate\" >= :fromDate",
            result.SqlText);
        Assert.Equal(["fromDate"], result.Parameters);
        Assert.Equal("fromDate", result.ParamList);
    }

    [Fact]
    public void A_parameter_runs_as_a_placeholder_and_never_as_text()
    {
        var result = Normalize("select DocEntry from OINV where DocTotal > :docTotal");

        Assert.Equal("select \"DocEntry\" from \"OINV\" where \"DocTotal\" > @docTotal", result.Executable);
    }

    [Theory]
    [InlineData("select itemcode, itemName from oitm where ItemCode > 'i01'")]
    [InlineData("select [itemcode], [itemName] from [oitm] where [ItemCode] > 'i01'")]
    [InlineData("select \"itemcode\", \"itemName\" from \"oitm\" where \"ItemCode\" > 'i01'")]
    public void Every_way_of_writing_a_name_gives_the_form_of_the_guide(string sql)
    {
        Assert.Equal("select \"ItemCode\", \"ItemName\" from \"OITM\" where \"ItemCode\" > 'i01'", Normalize(sql).SqlText);
    }

    [Fact]
    public void A_column_alias_keeps_its_letters_because_it_is_the_json_name()
    {
        Assert.Equal(
            "select t1.\"DocEntry\" as \"Col1\" , t1.\"DocNum\" as \"Col2\" from \"OINV\" t1 where t1.\"DocEntry\" > 0",
            Normalize("select t1.DocEntry as Col1 , t1.DocNum as Col2 from OINV t1 where t1.DocEntry > 0").SqlText);
    }

    [Fact]
    public void An_alias_may_be_written_without_as()
    {
        Assert.Equal("select \"DocEntry\" \"Col1\" from \"OINV\"", Normalize("select DocEntry Col1 from OINV").SqlText);
    }

    [Fact]
    public void Order_by_may_name_an_alias()
    {
        Assert.Equal(
            "select \"CardCode\", count(*) as \"Total\" from \"OINV\" group by \"CardCode\" order by \"Total\" desc",
            Normalize("select CardCode, count(*) as Total from OINV group by CardCode order by Total desc").SqlText);
    }

    [Fact]
    public void A_table_name_can_be_the_qualifier()
    {
        Assert.Equal("select \"OINV\".\"DocEntry\" from \"OINV\"", Normalize("select oinv.docentry from oinv").SqlText);
    }

    [Fact]
    public void Normalized_text_can_be_normalized_again_without_a_change()
    {
        var once = Normalize("select t0.docnum as Num from opch t0 inner join pch1 t1 on t0.docentry = t1.docentry where t0.docdate >= :from and ifnull(t1.itemcode, 'x') like 'a%'").SqlText;

        Assert.Equal(once, Normalize(once).SqlText);
    }

    [Fact]
    public void Column_names_of_each_table_keep_their_own_spelling()
    {
        Assert.Equal("select \"CANCELED\" from \"OINV\"", Normalize("select canceled from oinv").SqlText);
        Assert.Equal("select \"Canceled\" from \"ORCT\"", Normalize("select canceled from orct").SqlText);
    }

    [Fact]
    public void A_user_defined_field_is_an_ordinary_column()
    {
        Assert.Equal("select \"U_Region\" from \"OCRD\"", Normalize("select u_region from ocrd").SqlText);
    }

    [Theory]
    [InlineData("select t1.DocEntry, t1.DocNum from OINV t1 where not t1.DocEntry = 1 and t1.DocNum = 1 or t1.Comments <> '1234'")]
    [InlineData("select t1.DocEntry from OINV t1 where not (t1.DocEntry = 1 or t1.Comments <> '1234') and t1.DocNum = 1")]
    [InlineData("select DocEntry from OINV where DocEntry BETWEEN 1 AND 10")]
    [InlineData("select t1.DocEntry from OINV t1 order by t1.DocEntry")]
    [InlineData("select DocStatus, DocType, count(*) as GroupCount from OINV group by DocStatus, DocType having count(*) > 0")]
    [InlineData("select DocEntry, DocType from OINV t1 where DocEntry is not null and t1.Comments is null")]
    [InlineData("SELECT 1 as c1, 'string' as c2 FROM OITM")]
    [InlineData("select CardCode from OINV where CardCode like 'c%'")]
    [InlineData("select t1.LineNum from INV1 t1 union all select t1.DocEntry from PCH1 t1")]
    [InlineData("select DocEntry from OINV t1 where DocEntry in (select t2.DocEntry from INV1 t2) or DocEntry not in (select t2.DocEntry from PCH1 t2)")]
    [InlineData("select DocEntry, DocType from OINV t1 where exists(select 1 from INV1 t2 where t1.DocEntry = t2.DocEntry)")]
    [InlineData("select t1.DocEntry, t2.LineNum from OINV t1 inner join INV1 t2 on t1.DocEntry = t2.DocEntry")]
    [InlineData("select t1.DocEntry, t2.LineNum from OINV t1 left join INV1 t2 on t1.DocEntry = t2.DocEntry")]
    [InlineData("select t1.DocEntry, t2.LineNum from OINV t1 right join INV1 t2 on t1.DocEntry = t2.DocEntry")]
    [InlineData("select t1.DocEntry, t2.LineNum from OINV t1 FULL OUTER JOIN INV1 t2 on t1.DocEntry = t2.DocEntry")]
    [InlineData("select t1.DocEntry, t2.LineNum, t3.CardCode from OINV t1 inner join INV1 t2 on t1.DocEntry = t2.DocEntry left join OCRD t3 on t1.CardCode = t3.CardCode")]
    [InlineData("select sum(\"DocTotal\") as sumDocTotal, avg(\"DocTotal\") as avgDocTotal from OINV")]
    [InlineData("select min(\"DocTotal\") as minDocTotal, max(\"DocTotal\") as maxDocTotal from OINV")]
    [InlineData("select count(distinct docEntry) as countDistinct, count(*)as cnt from OINV")]
    [InlineData("SELECT ItemCode, lower(ItemCode) as lowerItemCode, upper(ItemCode) as upperItemCode FROM OITM where lower(ItemCode) = 'i001'")]
    [InlineData("SELECT ItemCode, right(ItemCode, 1) as rightItemCode, LEFT(ItemCode, 1) as leftItemCode FROM OITM")]
    [InlineData("select distinct CardCode from OINV")]
    [InlineData("select T0.Quantity * T0.Price as Amount from INV1 T0")]
    public void The_keywords_and_functions_of_the_guide_are_accepted(string sql)
    {
        Assert.NotEmpty(Normalize(sql).Executable);
    }

    [Fact]
    public void Top_stays_in_the_text_and_becomes_limit_to_run()
    {
        var result = Normalize("select top 2 DocStatus from OINV");

        Assert.Equal("select top 2 \"DocStatus\" from \"OINV\"", result.SqlText);
        Assert.DoesNotContain("top", result.Executable, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("from \"OINV\" LIMIT 2", result.Executable);
    }

    [Theory]
    [InlineData("isnull")]
    [InlineData("IFNULL")]
    public void Both_null_functions_become_one_as_in_the_guide(string function)
    {
        var result = Normalize($"select DocEntry, {function}(Comments, 'x') as c from OINV");

        Assert.Equal("select \"DocEntry\", IFNULL(\"Comments\", 'x') as \"c\" from \"OINV\"", result.SqlText);
        Assert.Equal("select \"DocEntry\", COALESCE(\"Comments\", 'x') as \"c\" from \"OINV\"", result.Executable);
    }

    [Fact]
    public void Select_all_is_allowed_inside_a_sub_query()
    {
        Assert.Contains("(select * from \"INV1\" t2", Normalize("select DocEntry, DocType from OINV t1 where exists (select * from INV1 t2 where t1.DocEntry = t2.DocEntry)").SqlText);
    }

    [Theory]
    [InlineData("select DocEntry from OINV where DocTotal > :a and DocTotal < :a", new[] { "a" })]
    [InlineData("select DocEntry from OINV where DocTotal > :a and CardCode = :b", new[] { "a", "b" })]
    public void Each_parameter_is_listed_once(string sql, string[] expected)
    {
        Assert.Equal(expected, Normalize(sql).Parameters);
    }

    [Fact]
    public void Without_parameters_the_list_is_empty()
    {
        Assert.Null(Normalize("select DocEntry from OINV").ParamList);
    }

    [Fact]
    public void A_string_stays_exactly_as_written()
    {
        Assert.Contains("'it''s; -- /* select'", Normalize("select DocEntry from OINV where Comments = 'it''s; -- /* select'").SqlText);
    }

    [Theory]
    [InlineData("update ORDR set Comments = '1234'", 0, "mismatched input 'update'")]
    [InlineData("delete from OINV", 0, "mismatched input 'delete'")]
    [InlineData("insert into OINV (DocEntry) values (1)", 0, "mismatched input 'insert'")]
    [InlineData("drop table OINV", 0, "mismatched input 'drop'")]
    [InlineData("truncate table OITM", 0, "mismatched input 'truncate'")]
    [InlineData("", 0, "mismatched input ''")]
    [InlineData("select * from OINV", 7, "asterisk")]
    [InlineData("select t1.* from OINV t1", 7, "asterisk")]
    [InlineData("select DocEntry from OINV t1 order by DocEntry dsc", 47, "Incorrect syntax near 'dsc'")]
    [InlineData("select \"ItemCode\", length(\"ItemCode\")as lenItemCode from oitm", 19, "Cannot support this function or expression: 'length'")]
    [InlineData("select max(ItemCode) as min_itemCode, min(ItemCode) as min_itemCode from OITM", 55, "Multiple name for column or column alias 'min_itemCode'")]
    public void Refusals_have_the_code_position_and_reason_of_the_guide(string sql, int position, string reason)
    {
        var error = Refused(sql);

        Assert.Equal("701", error.Code);
        Assert.Contains($"character position {position},", error.Message);
        Assert.Contains(reason, error.Message);
    }

    [Theory]
    [InlineData("select count(*) from OINV")]
    [InlineData("select DocEntry from OINV; drop table OINV")]
    [InlineData("select DocEntry from OINV;")]
    [InlineData("select DocEntry from OINV -- comment")]
    [InlineData("select DocEntry /* comment */ from OINV")]
    [InlineData("select DocEntry from OINV where DocDate > '2026-01-01'::date")]
    [InlineData("select DocEntry from OINV where CardCode = 'not closed")]
    [InlineData("select \"DocEntry from OINV")]
    [InlineData("select DocEntry from OINV limit 5")]
    [InlineData("select DocEntry from OINV offset 5")]
    [InlineData("select DocEntry into x from OINV")]
    [InlineData("select DocEntry from OINV for update")]
    [InlineData("select pg_sleep(10) as x from OITM")]
    [InlineData("select version() as x from OITM")]
    [InlineData("select lo_import('/etc/passwd') as x from OITM")]
    [InlineData("select 1 as x")]
    [InlineData("select DocEntry from (select DocEntry from OINV) x")]
    [InlineData("select (DocEntry from OINV")]
    [InlineData("select DocEntry) from OINV")]
    [InlineData("select DocEntry as \"x\"\"; drop table y; --\" from OINV")]
    [InlineData("select DocEntry as \"a b\" from OINV")]
    [InlineData("select DocEntry from OINV \"a b\"")]
    [InlineData("select top 1.5 DocEntry from OINV")]
    [InlineData("select top x DocEntry from OINV")]
    [InlineData("select DocEntry from OINV where DocEntry in (select top 1 DocEntry from INV1)")]
    [InlineData("select top 1 DocEntry from OINV union select DocEntry from INV1")]
    [InlineData("select case when DocEntry = 1 then 1 end as x from OINV")]
    [InlineData("select x.DocEntry from OINV t1")]
    [InlineData("select DocEntry, from OINV")]
    public void Anything_else_is_a_syntax_error(string sql)
    {
        Assert.Equal("701", Refused(sql).Code);
    }

    [Theory]
    [InlineData("select CardCode from OCRD t1 where exists(select 1 from CRD2 t2 where t1.CardCode = t2.CardCode)", "CRD2")]
    [InlineData("select U_NAME from OUSR", "OUSR")]
    [InlineData("select U_NAME from ousr", "ousr")]
    [InlineData("select \"U_NAME\" from \"OUSR\"", "OUSR")]
    [InlineData("select [U_NAME] from [OUSR]", "OUSR")]
    [InlineData("select firstName from OHEM", "OHEM")]
    [InlineData("select AcctCode from OACT", "OACT")]
    [InlineData("select SqlText from OSQL", "OSQL")]
    [InlineData("select TableID from CUFD", "CUFD")]
    [InlineData("select usename from pg_user", "pg_user")]
    [InlineData("select usename from pg_catalog.pg_user", "pg_catalog")]
    [InlineData("select DocEntry from public.OINV", "public")]
    [InlineData("select DocEntry from OINV union all select U_NAME from OUSR", "OUSR")]
    [InlineData("select DocEntry from OINV, OUSR", "OUSR")]
    [InlineData("select t1.DocEntry from OINV t1 inner join OUSR t2 on t1.DocEntry = t2.USERID", "OUSR")]
    public void A_table_outside_the_list_is_refused_wherever_it_stands(string sql, string table)
    {
        var error = Refused(sql);

        Assert.Equal("702", error.Code);
        Assert.Equal($"Table '{table}' not accessible", error.Message);
    }

    [Fact]
    public void A_table_on_the_list_that_the_database_does_not_have_is_refused()
    {
        var empty = new Dictionary<string, IReadOnlyList<string>>();

        var error = Assert.Throws<ServiceLayerException>(() => SqlNormalizer.Normalize("select DocEntry from OINV", empty));

        Assert.Equal("702", error.Code);
    }

    [Theory]
    [InlineData("select Algo from OITM", "Column 'Algo' from table 'OITM' not accessible")]
    [InlineData("select t1.Algo from OINV t1", "Column 'Algo' from table 'OINV' not accessible")]
    [InlineData("select DocEntry from OINV where Algo = 1", "Column 'Algo' from table 'OINV' not accessible")]
    [InlineData("select current_user as u from OITM", "Column 'current_user' from table 'OITM' not accessible")]
    [InlineData("select DocEntry from OINV where DocEntry = current_setting", "Column 'current_setting' from table 'OINV' not accessible")]
    public void A_column_outside_the_catalog_is_refused(string sql, string message)
    {
        var error = Refused(sql);

        Assert.Equal("703", error.Code);
        Assert.Equal(message, error.Message);
    }
}
