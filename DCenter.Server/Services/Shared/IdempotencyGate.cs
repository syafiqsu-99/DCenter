using System.Collections.Concurrent;
using DCenter.Server.Models;

namespace DCenter.Server.Services;

// Replays the first successful result for a repeated Idempotency-Key so a resubmitted or retried
// stock transaction is recorded once. In-memory: one IIS worker process, keys kept for a day.
public sealed class IdempotencyGate(TimeProvider clock)
{
    public const string Header = "Idempotency-Key";
    public const int MaxKeyLength = 100;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private sealed record Entry(Lazy<Task<object>> Work, DateTimeOffset CreatedAt);

    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private long lastSweepTicks;

    public IdempotencyGate() : this(TimeProvider.System) { }

    public int Count => entries.Count;

    public async Task<ServiceResult<T>> RunAsync<T>(string scope, string? key, Func<Task<ServiceResult<T>>> action)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength) return await action();

        Sweep();
        var id = $"{scope}|{key.Trim()}";
        var entry = entries.GetOrAdd(id, _ => new Entry(
            new Lazy<Task<object>>(async () => await action(), LazyThreadSafetyMode.ExecutionAndPublication),
            clock.GetUtcNow()));

        try
        {
            var result = (ServiceResult<T>)await entry.Work.Value;
            if (!result.Succeeded) entries.TryRemove(new KeyValuePair<string, Entry>(id, entry));
            return result;
        }
        catch
        {
            entries.TryRemove(new KeyValuePair<string, Entry>(id, entry));
            throw;
        }
    }

    private void Sweep()
    {
        var now = clock.GetUtcNow();
        var last = Interlocked.Read(ref lastSweepTicks);
        if (now.UtcTicks - last < TimeSpan.FromMinutes(10).Ticks) return;
        if (Interlocked.CompareExchange(ref lastSweepTicks, now.UtcTicks, last) != last) return;

        foreach (var (id, entry) in entries)
        {
            if (now - entry.CreatedAt > Lifetime) entries.TryRemove(new KeyValuePair<string, Entry>(id, entry));
        }
    }
}
