using DCenter.Server.Services;

namespace DCenter.Server.Tests;

public class StockImportParserTests
{
    private static readonly DateOnly Today = new(2026, 3, 15);

    [Theory]
    [InlineData(null, "2026-03-15", 0)]
    [InlineData("2026-03-01", "2026-03-01", 0)]
    [InlineData("1/3/2026", "2026-03-01", 0)]
    [InlineData("2026-03-16", "2026-03-16", 1)]
    public void ParseDate_AcceptsKnownFormatsAndFlagsFutureDates(string? raw, string expected, int messageCount)
    {
        var messages = new List<string>();

        var date = StockImportService.ParseDate(raw, Today, messages);

        Assert.Equal(DateOnly.Parse(expected), date);
        Assert.Equal(messageCount, messages.Count);
    }

    [Fact]
    public void ParseDate_InvalidTextIsReportedWithAnExampleDate()
    {
        var messages = new List<string>();

        var date = StockImportService.ParseDate("next week", Today, messages);

        Assert.Equal(default, date);
        Assert.Contains("2026-03-15", Assert.Single(messages));
    }

    [Theory]
    [InlineData("5", 5.00, 0)]
    [InlineData("5.004 kg", 5.00, 0)]
    [InlineData("0", 0, 1)]
    [InlineData("five", 0, 1)]
    [InlineData(null, 0, 1)]
    public void ParseQuantity_RoundsAndValidates(string? raw, double expected, int messageCount)
    {
        var messages = new List<string>();

        var qty = StockImportService.ParseQuantity(raw, messages);

        Assert.Equal((decimal)expected, qty);
        Assert.Equal(messageCount, messages.Count);
    }

    [Fact]
    public void ParseLot_UppercasesAndLimitsLength()
    {
        var messages = new List<string>();
        Assert.Equal("L12 345", StockImportService.ParseLot(" l12  345 ", messages));

        var tooLong = new List<string>();
        StockImportService.ParseLot(new string('x', 61), tooLong);
        Assert.Equal("Lot / Heat No. is limited to 60 characters.", Assert.Single(tooLong));
    }

    [Fact]
    public void ParseBrand_PrefersTheKnownSpelling()
    {
        var messages = new List<string>();
        var brands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["kobelco"] = "KOBELCO" };

        Assert.Equal("KOBELCO", StockImportService.ParseBrand("kobelco", brands, messages));
        Assert.Equal("Lincoln", StockImportService.ParseBrand("lincoln", brands, []));
        Assert.Null(StockImportService.ParseBrand(null, brands, messages));
    }

    [Fact]
    public void ParseOption_FallsBackAndExplainsUnknownValues()
    {
        var messages = new List<string>();

        Assert.Equal("Normal", StockImportService.ParseOption(null, "Storage", ["Normal", "Activated"], "Normal", messages));
        Assert.Empty(messages);
        Assert.Equal("Normal", StockImportService.ParseOption("Frozen", "Storage", ["Normal", "Activated"], "Normal", messages));
        Assert.Single(messages);
    }
}
