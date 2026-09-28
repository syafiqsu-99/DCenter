using System.Text;
using DCenter.Server.Services;

namespace DCenter.Server.Tests;

public class CsvTextTests
{
    [Fact]
    public void Parse_HandlesQuotesEscapesAndBlankLines()
    {
        var rows = CsvText.Parse("a,\"b,c\",\"d\"\"e\"\r\n\r\nx,y\n", out var error);

        Assert.Null(error);
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b,c", "d\"e"], rows[0].Fields);
        Assert.Equal(1, rows[0].Line);
        Assert.Equal(["x", "y"], rows[1].Fields);
        Assert.Equal(3, rows[1].Line);
    }

    [Fact]
    public void Parse_KeepsLineBreaksInsideQuotes()
    {
        var rows = CsvText.Parse("\"line1\nline2\",z", out _);

        Assert.Equal(["line1\nline2", "z"], rows[0].Fields);
    }

    [Fact]
    public void Parse_ReportsAnUnclosedQuote()
    {
        CsvText.Parse("a,b\nc,\"oops\nd", out var error);

        Assert.NotNull(error);
        Assert.Contains("line 2", error);
    }

    [Fact]
    public void Write_GuardsFormulasButNotNumbers()
    {
        var text = Encoding.UTF8.GetString(CsvText.Write([["=cmd|' /C calc'!A0", "-5.00", "+60123", "a,b"]]));

        Assert.Equal("\uFEFF'=cmd|' /C calc'!A0,-5.00,+60123,\"a,b\"\r\n", text);
    }

    [Theory]
    [InlineData("'=SUM(A1)", "=SUM(A1)")]
    [InlineData("'abc", "'abc")]
    [InlineData("plain", "plain")]
    public void Unguard_RemovesOnlyTheFormulaGuard(string input, string expected)
    {
        Assert.Equal(expected, CsvText.Unguard(input));
    }

    [Fact]
    public void Decode_FallsBackToLatin1ForNonUtf8Files()
    {
        Assert.Equal("Café", CsvText.Decode([0x43, 0x61, 0x66, 0xE9]));
    }
}
