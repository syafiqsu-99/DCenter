using DCenter.Server.Models;
using DCenter.Server.Services;
using static DCenter.Server.Services.ReferenceCsv;

namespace DCenter.Server.Tests;

public class SettingsRulesTests
{
    [Fact]
    public void LengthError_NamesTheFirstColumnThatIsTooLong()
    {
        var columns = new BpvcTable().Columns;
        var values = new string?[columns.Length];
        values[0] = "SA-516";
        values[3] = new string('1', 51);

        Assert.Equal("P-No. is limited to 50 characters.", LengthError(columns, values));
        values[3] = "1";
        Assert.Null(LengthError(columns, values));
    }

    [Fact]
    public void Upsert_SkipsRowsWithValuesLongerThanTheColumn()
    {
        var columns = new WpsTable().Columns;
        var rows = new List<string?[]> { new[] { "WPS-1", "1", new string('x', 201), null } };

        var counts = Upsert(columns, rows, new List<string?[]>(), r => r, (_, _) => { }, () => new string?[4]);

        Assert.Equal(new ImportCounts(0, 0, 0, 1), counts);
    }

    [Theory]
    [InlineData("wps.csv", true)]
    [InlineData("WPS.CSV", true)]
    [InlineData("wps.xlsx", false)]
    public async Task ReadAsync_AcceptsOnlyCsvFiles(string fileName, bool accepted)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("WpsNo,PNo\nA,1\n");
        var file = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);

        var sheet = await ReadAsync(file, new WpsTable().Columns, CancellationToken.None);

        if (accepted) Assert.Empty(sheet.Errors);
        else Assert.Equal(["Only .csv files can be imported."], sheet.Errors);
    }

    [Theory]
    [InlineData("Process", "", "", "Process", "0", "1")]
    [InlineData("Unknown", "5", "no", null, "5", "0")]
    [InlineData("Size", "x", "TRUE", "Size", "0", "1")]
    public void LookupRows_AreNormalizedBeforeComparing(string category, string sort, string active, string? expectedCategory, string expectedSort, string expectedActive)
    {
        var row = LookupService.NormalizeRow([category, "3.2", sort, active]);

        Assert.Equal(new[] { expectedCategory, "3.2", expectedSort, expectedActive }, row);
    }

    [Fact]
    public void LookupValidate_EnforcesCategoryAndLength()
    {
        Assert.NotNull(LookupService.Validate(new LookupUpsert("Nope", "x", 0, true)).Error);
        Assert.Equal("Value is required.", LookupService.Validate(new LookupUpsert("Size", "  ", 0, true)).Error);
        Assert.Equal("Value is limited to 200 characters.", LookupService.Validate(new LookupUpsert("Size", new string('x', 201), 0, true)).Error);
        Assert.Null(LookupService.Validate(new LookupUpsert(" Size ", " 3.2 ", 0, true)).Error);
    }

    [Fact]
    public void ServiceResultNotFound_IsAFailureWithoutMessage()
    {
        var result = ServiceResult<bool>.NotFound();

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.Status);
        Assert.Equal(string.Empty, result.Error);
    }
}
