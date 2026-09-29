using DCenter.Server.Services;
using Cat = DCenter.Server.Entities.StockCatalog;

namespace DCenter.Server.Tests;

public class ConsumableGuardsTests
{
    private static readonly DateOnly Today = new(2026, 3, 15);

    private static ItemRef Electrode => new(1, Cat.ElectrodeFiller, "3.20 E7018", null, true, Cat.OvenMildSteel);
    private static ItemRef Filler => new(2, Cat.BarePowderFiller, "2.40 ER70S-6", null, true, null);

    [Fact]
    public void Take_SpecificLotWithinBalance()
    {
        var (lines, error) = ConsumableGuards.Take([(4, 3m), (9, 2m)], 9, 1.5m, "Normal storage");

        Assert.Null(error);
        Assert.Equal([new StockLine(9, 1.5m)], lines);
    }

    [Fact]
    public void Take_SpecificLotOverBalanceIsRefused()
    {
        var (lines, error) = ConsumableGuards.Take([(4, 3m), (9, 2m)], 9, 2.5m, "Normal storage");

        Assert.Null(lines);
        Assert.Contains("2.00 kg", error);
    }

    [Fact]
    public void Take_WithoutLotUsesFifo()
    {
        var (lines, _) = ConsumableGuards.Take([(9, 2m), (4, 3m)], null, 4m, "Activated storage");

        Assert.Equal([new StockLine(4, 3m), new StockLine(9, 1m)], lines);
    }

    [Fact]
    public void Take_ShortfallMessageIgnoresNegativeLots()
    {
        var (lines, error) = ConsumableGuards.Take([(1, 2m), (2, -1m)], null, 5m, "compartment MS-1");

        Assert.Null(lines);
        Assert.Equal("Only 2.00 kg is in compartment MS-1.", error);
    }

    [Fact]
    public void ResolveBin_AllowsCompartmentsForElectrodesOnly()
    {
        Assert.Equal((10, null), ConsumableGuards.ResolveBin(Electrode, 10));
        Assert.Equal((null, null), ConsumableGuards.ResolveBin(Filler, null));
        Assert.NotNull(ConsumableGuards.ResolveBin(Filler, 10).Error);
    }

    [Fact]
    public void Common_RequiresAUser()
    {
        Assert.NotNull(ConsumableGuards.Common("  ", null, Today).Error);
    }

    [Fact]
    public void Common_RejectsFutureDates()
    {
        Assert.Equal("Date cannot be in the future.", ConsumableGuards.Common("Ali", Today.AddDays(1), Today).Error);
    }

    [Fact]
    public void Common_DefaultsToToday()
    {
        var (user, date, error) = ConsumableGuards.Common(" Ali  Bin ", null, Today);

        Assert.Null(error);
        Assert.Equal("Ali Bin", user);
        Assert.Equal(Today, date);
    }

    [Theory]
    [InlineData(7, false, true)]
    [InlineData(8, false, false)]
    [InlineData(0, false, true)]
    [InlineData(365, true, true)]
    public void BackdateError_LimitsWelderEntriesToTheWindow(int daysBack, bool supervisor, bool allowed)
    {
        var today = new DateOnly(2026, 9, 28);

        var error = ConsumableGuards.BackdateError(today.AddDays(-daysBack), today, supervisor, 7);

        Assert.Equal(allowed, error is null);
    }

    [Fact]
    public void BackdateError_ZeroDaysMeansTodayOnly()
    {
        var today = new DateOnly(2026, 9, 28);

        Assert.Null(ConsumableGuards.BackdateError(today, today, false, 0));
        Assert.Contains("today", ConsumableGuards.BackdateError(today.AddDays(-1), today, false, 0));
    }

    [Fact]
    public void ReceiverName_RequiresAChosenActiveWelder()
    {
        Assert.Contains("Received By", ConsumableGuards.ReceiverName(null, null).Error);
        Assert.Contains("no longer", ConsumableGuards.ReceiverName(5, null).Error);
        Assert.Contains("reactivate", ConsumableGuards.ReceiverName(5, new ReceiverRef("Ali", false)).Error);
        Assert.Equal(("Ali", null), ConsumableGuards.ReceiverName(5, new ReceiverRef("Ali", true)));
    }

    [Theory]
    [InlineData("W-01", "Ali", true)]
    [InlineData("W-01", null, false)]
    [InlineData("", "Ali", false)]
    public void DuplicateWelderNumber_IsRefusedOnlyWhenAlreadyRegistered(string number, string? registeredTo, bool refused)
    {
        var error = DCenter.Server.Controllers.WeldersController.DuplicateNumberError(number, registeredTo);

        Assert.Equal(refused, error is not null);
    }
}
