namespace DCenter.Server.Services;

// Local wall-clock helpers; with TimeProvider.System these equal DateTime.Now / DateTime.Today.
public static class Clock
{
    public static DateTime LocalNow(this TimeProvider time) => DateTime.SpecifyKind(time.GetLocalNow().DateTime, DateTimeKind.Local);

    public static DateOnly Today(this TimeProvider time) => DateOnly.FromDateTime(time.LocalNow());
}
