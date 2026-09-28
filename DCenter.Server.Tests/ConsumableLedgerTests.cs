using DCenter.Server.Entities;
using DCenter.Server.Services;
using Cat = DCenter.Server.Entities.StockCatalog;

namespace DCenter.Server.Tests;

public class ConsumableLedgerTests
{
    [Fact]
    public void AllocateFifo_FillsFromOldestLotFirst()
    {
        var lines = ConsumableLedger.AllocateFifo([(7, 2m), (3, 1.5m), (5, 4m)], 3m);

        Assert.NotNull(lines);
        Assert.Equal([(3, 1.5m), (5, 1.5m)], lines);
    }

    [Fact]
    public void AllocateFifo_SkipsEmptyAndNegativeLots()
    {
        var lines = ConsumableLedger.AllocateFifo([(1, 0m), (2, -1m), (3, 2m)], 2m);

        Assert.Equal([(3, 2m)], lines);
    }

    [Fact]
    public void AllocateFifo_ReturnsNullWhenStockIsShort()
    {
        Assert.Null(ConsumableLedger.AllocateFifo([(1, 1m), (2, 1m)], 2.01m));
    }

    [Fact]
    public void AllocateFifo_ZeroQuantityTakesNothing()
    {
        Assert.Empty(ConsumableLedger.AllocateFifo([(1, 5m)], 0m)!);
    }

    public static TheoryData<DateTime?, DateTime?, DateTime?, DateTime?, bool, bool, decimal, string> StatusCases()
    {
        var t0 = new DateTime(2026, 9, 1, 8, 0, 0);
        return new()
        {
            { null, null, null, null, false, false, 0m, Cat.StatusCancelled },
            { null, null, null, null, true, false, 5m, Cat.StatusQueued },
            { t0, null, null, null, true, false, 5m, Cat.StatusBaking },
            { t0, t0.AddHours(2), null, null, true, false, 5m, Cat.StatusBaked },
            { t0, t0.AddHours(2), null, null, true, false, 0m, Cat.StatusClosed },
            { t0, t0.AddHours(2), null, null, true, true, 1m, Cat.StatusRebakeQueued },
            { t0, t0.AddHours(2), t0.AddHours(5), null, true, true, 1m, Cat.StatusRebaking },
            { t0, t0.AddHours(2), t0.AddHours(5), t0.AddHours(6), true, true, 1m, Cat.StatusRebaked },
            { t0, t0.AddHours(2), t0.AddHours(5), t0.AddHours(6), true, true, 0m, Cat.StatusClosed },
        };
    }

    [Theory]
    [MemberData(nameof(StatusCases))]
    public void DeriveStatus_FollowsTheBakingLifecycle(
        DateTime? bakeStart, DateTime? bakeStop, DateTime? rebakeStart, DateTime? rebakeStop,
        bool sent, bool rebakeReturned, decimal balance, string expected)
    {
        var record = new BakingRecord { BakeStart = bakeStart, BakeStop = bakeStop, RebakeStart = rebakeStart, RebakeStop = rebakeStop };

        Assert.Equal(expected, ConsumableLedger.DeriveStatus(record, sent, rebakeReturned, balance));
    }
}
