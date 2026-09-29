using DCenter.Server.Services;
using static DCenter.Server.Services.ReferenceCsv;

namespace DCenter.Server.Tests;

public class ReferenceCsvTests
{
    private static readonly Column[] Columns =
    [
        new("SpecNo", Required: true, Key: true, "SpecNoRaw", "SpecNo"),
        new("Designation", Required: false, Key: true, "Designation", "Grade"),
        new("PNo", Required: true, Key: true),
        new("GroupNo", Required: false, Key: false),
    ];

    private sealed class Row
    {
        public string?[] V { get; set; } = [];
    }

    private static ImportCounts Run(List<Row> table, string csv)
    {
        var sheet = Read(csv, Columns);
        Assert.Empty(sheet.Errors);
        return Upsert(Columns, sheet.Rows, table.ToList(), r => r.V, (r, v) => r.V = [.. v], () =>
        {
            var r = new Row();
            table.Add(r);
            return r;
        });
    }

    [Fact]
    public void Key_IgnoresCaseWhitespaceAndNullVsEmpty()
    {
        Assert.Equal(Key("SA-516 ", "grade  70", null), Key("sa-516", "Grade 70", ""));
        Assert.NotEqual(Key("SA-516", "70", "1"), Key("SA-516", "70", "3"));
    }

    [Fact]
    public void Read_MapsColumnsByHeaderNameInAnyOrder()
    {
        var sheet = Read("P-No.,Group No.,Grade,Spec No.\n1,2,70,SA-516\n", Columns);

        Assert.Empty(sheet.Errors);
        Assert.Equal(new string?[] { "SA-516", "70", "1", "2" }, sheet.Rows[0]);
    }

    [Fact]
    public void Read_PrefersLegacySpecNoRawAndFallsBackToSpecNo()
    {
        var sheet = Read("SpecNoRaw,SpecNo,Designation,PNo\nSA-516,SA516,70,1\n,SA106,B,1\n", Columns);

        Assert.Empty(sheet.Errors);
        Assert.Equal("SA-516", sheet.Rows[0][0]);
        Assert.Equal("SA106", sheet.Rows[1][0]);
    }

    [Fact]
    public void Read_ReportsMissingAndDuplicateColumns()
    {
        var sheet = Read("SpecNo,Designation,Designation\nSA-516,70,70\n", Columns);

        Assert.Contains(sheet.Errors, e => e.Contains("\"PNo\""));
        Assert.Contains(sheet.Errors, e => e.Contains("more than once"));
    }

    [Fact]
    public void Read_UnguardsFormulaEscapedCells()
    {
        var sheet = Read("SpecNo,Designation,PNo\nSA-516,'-,1\n", Columns);

        Assert.Equal("-", sheet.Rows[0][1]);
    }

    [Fact]
    public void Upsert_UpdatesOnKeyMatchAndInsertsOnKeyChange()
    {
        var table = new List<Row> { new() { V = ["SA-516", "70", "1", "1"] } };

        var counts = Run(table, "SpecNo,Designation,PNo,GroupNo\nsa-516,70,1,2\nSA-516,60,1,1\n");

        Assert.Equal(new ImportCounts(1, 1, 0, 0), counts);
        Assert.Equal(2, table.Count);
        Assert.Equal(new string?[] { "sa-516", "70", "1", "2" }, table[0].V);
        Assert.Equal(new string?[] { "SA-516", "60", "1", "1" }, table[1].V);
    }

    [Fact]
    public void Upsert_CountsUnchangedAndSkipsRowsMissingRequiredKeys()
    {
        var table = new List<Row> { new() { V = ["SA-516", null, "1", ""] } };

        var counts = Run(table, "SpecNo,Designation,PNo,GroupNo\nSA-516,,1,\n,70,1,\nSA-106,B,,\n");

        Assert.Equal(new ImportCounts(0, 0, 1, 2), counts);
        Assert.Single(table);
    }

    [Fact]
    public void Upsert_RepeatedKeyInSameFileUpdatesTheNewRow()
    {
        var table = new List<Row>();

        var counts = Run(table, "SpecNo,Designation,PNo,GroupNo\nSA-516,70,1,1\nSA-516,70,1,2\n");

        Assert.Equal(new ImportCounts(1, 1, 0, 0), counts);
        Assert.Single(table);
        Assert.Equal("2", table[0].V[3]);
    }

    [Fact]
    public void UpsertPlan_SeparatesChangedRowsFromNewRowsInFileOrder()
    {
        var existing = new List<Row> { new() { V = ["SA-516", "70", "1", "1"] }, new() { V = ["SA-106", "B", "1", "1"] } };
        var sheet = Read("SpecNo,Designation,PNo,GroupNo\nSA-516,70,1,2\nSA-333,6,1,1\nSA-516,70,1,3\nSA-106,B,1,1\nSA-240,304,8,1\n", Columns);

        var plan = UpsertPlan(Columns, sheet.Rows, existing, r => r.V, (r, v) => r.V = [.. v], () => new Row());

        Assert.Equal(new ImportCounts(2, 2, 1, 0), plan.Counts);
        Assert.Same(existing[0], Assert.Single(plan.Updated));
        Assert.Equal("3", existing[0].V[3]);
        Assert.Equal(["SA-333", "SA-240"], plan.Added.Select(r => r.V[0]));
    }
}
