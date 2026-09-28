using DCenter.Server.Models;
using DCenter.Server.Services;

namespace DCenter.Server.Tests;

public class IdempotencyGateTests
{
    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task SameKeyRunsOnceAndReplaysTheResult()
    {
        var gate = new IdempotencyGate();
        var runs = 0;
        Task<ServiceResult<string>> Action() { runs++; return Task.FromResult(ServiceResult<string>.Ok($"CT-{runs}")); }

        var first = await gate.RunAsync("POST /receive|Ali", "key-1", Action);
        var second = await gate.RunAsync("POST /receive|Ali", "key-1", Action);

        Assert.Equal(1, runs);
        Assert.Equal("CT-1", first.Value);
        Assert.Equal("CT-1", second.Value);
    }

    [Fact]
    public async Task ConcurrentDuplicatesShareOneExecution()
    {
        var gate = new IdempotencyGate();
        var runs = 0;
        var release = new TaskCompletionSource();
        async Task<ServiceResult<int>> Action()
        {
            Interlocked.Increment(ref runs);
            await release.Task;
            return ServiceResult<int>.Ok(42);
        }

        var a = gate.RunAsync("POST /issue|Ali", "k", Action);
        var b = gate.RunAsync("POST /issue|Ali", "k", Action);
        release.SetResult();

        Assert.Equal([42, 42], (await Task.WhenAll(a, b)).Select(r => r.Value));
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task FailuresAreNotCachedSoACorrectedResubmitRuns()
    {
        var gate = new IdempotencyGate();
        var runs = 0;
        Task<ServiceResult<int>> Action()
        {
            runs++;
            return Task.FromResult(runs == 1 ? ServiceResult<int>.Fail("Quantity must be greater than 0.") : ServiceResult<int>.Ok(7));
        }

        Assert.False((await gate.RunAsync("s", "k", Action)).Succeeded);
        Assert.Equal(7, (await gate.RunAsync("s", "k", Action)).Value);
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task ExceptionsAreNotCached()
    {
        var gate = new IdempotencyGate();
        var runs = 0;
        Task<ServiceResult<int>> Action()
        {
            runs++;
            return runs == 1 ? throw new TimeoutException("busy") : Task.FromResult(ServiceResult<int>.Ok(1));
        }

        await Assert.ThrowsAsync<TimeoutException>(() => gate.RunAsync("s", "k", Action));
        Assert.True((await gate.RunAsync("s", "k", Action)).Succeeded);
    }

    [Fact]
    public async Task KeysAreScopedByRouteAndUser()
    {
        var gate = new IdempotencyGate();
        var runs = 0;
        Task<ServiceResult<int>> Action() => Task.FromResult(ServiceResult<int>.Ok(++runs));

        await gate.RunAsync("POST /issue|Ali", "k", Action);
        await gate.RunAsync("POST /issue|Bala", "k", Action);
        await gate.RunAsync("POST /return|Ali", "k", Action);

        Assert.Equal(3, runs);
    }

    [Fact]
    public async Task MissingOrOversizedKeysAlwaysRun()
    {
        var gate = new IdempotencyGate();
        var runs = 0;
        Task<ServiceResult<int>> Action() => Task.FromResult(ServiceResult<int>.Ok(++runs));

        await gate.RunAsync("s", null, Action);
        await gate.RunAsync("s", "", Action);
        await gate.RunAsync("s", new string('x', IdempotencyGate.MaxKeyLength + 1), Action);
        await gate.RunAsync("s", new string('x', IdempotencyGate.MaxKeyLength + 1), Action);

        Assert.Equal(4, runs);
        Assert.Equal(0, gate.Count);
    }

    [Fact]
    public async Task EntriesExpireAfterTheirLifetime()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero));
        var gate = new IdempotencyGate(clock);
        var runs = 0;
        Task<ServiceResult<int>> Action() => Task.FromResult(ServiceResult<int>.Ok(++runs));

        await gate.RunAsync("s", "k", Action);
        clock.Now += IdempotencyGate.Lifetime + TimeSpan.FromHours(1);
        await gate.RunAsync("s", "k", Action);

        Assert.Equal(2, runs);
    }
}
