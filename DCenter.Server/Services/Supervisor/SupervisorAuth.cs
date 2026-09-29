using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DCenter.Server.Services;

public sealed record SupervisorSession(string Name, DateTimeOffset ExpiresAt);

public class SupervisorAuth(IDataProtectionProvider provider, IOptions<ConsumableOptions> options, TimeProvider time)
{
    public const string TokenHeader = "X-Supervisor-Token";

    private readonly ITimeLimitedDataProtector protector =
        provider.CreateProtector("DCenter.Consumables.Supervisor.v2").ToTimeLimitedDataProtector();

    private readonly int sessionHours = Math.Clamp(options.Value.SupervisorSessionHours, 1, 24);

    private long notBeforeMs;

    private readonly ConcurrentDictionary<string, DateTimeOffset> revoked = new(StringComparer.Ordinal);

    // Logout: the token stops working even though it has not expired. Kept in memory until it would have expired.
    public void Revoke(string? token)
    {
        var session = Validate(token);
        if (session is null) return;
        foreach (var (key, expiresAt) in revoked)
            if (expiresAt <= time.GetUtcNow()) revoked.TryRemove(key, out _);
        revoked[Fingerprint(token!)] = session.ExpiresAt;
    }

    private static string Fingerprint(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public (string Token, DateTimeOffset ExpiresAt) Issue(string name)
    {
        var now = time.GetUtcNow();
        var expiresAt = now.AddHours(sessionHours);
        var payload = $"{now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}|{name}";
        return (protector.Protect(payload, expiresAt), expiresAt);
    }

    public void RevokeIssuedBefore(DateTimeOffset moment)
    {
        var ms = moment.ToUnixTimeMilliseconds();
        long current;
        do
        {
            current = Interlocked.Read(ref notBeforeMs);
            if (ms <= current) return;
        }
        while (Interlocked.CompareExchange(ref notBeforeMs, ms, current) != current);
    }

    public SupervisorSession? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        if (!revoked.IsEmpty && revoked.ContainsKey(Fingerprint(token))) return null;
        try
        {
            var payload = protector.Unprotect(token, out var expiresAt);
            var separator = payload.IndexOf('|');
            if (separator <= 0
                || !long.TryParse(payload.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var issuedMs)
                || issuedMs < Interlocked.Read(ref notBeforeMs))
                return null;
            return new SupervisorSession(payload[(separator + 1)..], expiresAt);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static readonly object RequestCacheKey = new();

    // Validated once per request; the controller base, filters and guards all ask.
    public SupervisorSession? FromRequest(HttpRequest request)
    {
        var items = request.HttpContext.Items;
        if (items.TryGetValue(RequestCacheKey, out var cached)) return (SupervisorSession?)cached;
        var session = Validate(request.Headers[TokenHeader].ToString());
        items[RequestCacheKey] = session;
        return session;
    }
}
