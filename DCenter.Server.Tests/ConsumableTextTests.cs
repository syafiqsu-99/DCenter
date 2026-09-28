using DCenter.Server.Services;
using Cat = DCenter.Server.Entities.StockCatalog;

namespace DCenter.Server.Tests;

public class ConsumableTextTests
{
    [Theory]
    [InlineData(1.005, 1.01)]
    [InlineData(2.004, 2.00)]
    [InlineData(-0.005, -0.01)]
    public void RoundKg_RoundsHalfAwayFromZero(decimal input, decimal expected)
    {
        Assert.Equal(expected, ConsumableText.RoundKg(input));
    }

    [Theory]
    [InlineData("3.2mm", Cat.ElectrodeFiller, "3.20")]
    [InlineData("3,2", Cat.ElectrodeFiller, "3.20")]
    [InlineData(" 4 MM ", Cat.ElectrodeFiller, "4.00")]
    [InlineData("0", Cat.ElectrodeFiller, null)]
    [InlineData("100", Cat.ElectrodeFiller, null)]
    [InlineData("100/325", Cat.ElectrodeFiller, null)]
    [InlineData("100/325 mesh", Cat.BarePowderFiller, "100/325")]
    [InlineData("abc", Cat.BarePowderFiller, null)]
    public void Diameter_NormalisesSizes(string raw, string category, string? expected)
    {
        Assert.Equal(expected, ConsumableText.Diameter(raw, category));
    }

    [Fact]
    public void MatchOption_ExactIgnoresCaseAndSpacing()
    {
        Assert.Equal((Cat.Activated, null), ConsumableText.MatchOption(" activated ", Cat.ActiveStages));
    }

    [Fact]
    public void MatchOption_SuggestsCloseSpelling()
    {
        var (value, suggestion) = ConsumableText.MatchOption("Normall", Cat.ActiveStages);

        Assert.Null(value);
        Assert.Equal(Cat.Normal, suggestion);
    }

    [Fact]
    public void FreeText_CollapsesSpacesAndTruncates()
    {
        Assert.Equal("a b", ConsumableText.FreeText("  a    b ", 10));
        Assert.Equal("abc", ConsumableText.FreeText("abcdef", 3));
        Assert.Null(ConsumableText.FreeText("   ", 10));
    }
}
