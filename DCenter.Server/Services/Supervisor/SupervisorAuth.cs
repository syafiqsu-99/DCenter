using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DCenter.Server.Services;

public sealed record SupervisorSession(string Name, DateTimeOffset ExpiresAt)
{
    public DateTimeOffset LoginAt { get; init; }
}

public sealed record RevokedToken(string Fingerprint, DateTimeOffset ExpiresAt);

public class SupervisorAuth(IDataProtectionProvider provider, IOptions<ConsumableOptions> options, TimeProvider time)
{
    public const string TokenHeader = "X-Supervisor-Token";

    private readonly ITimeLimitedDataProtector protector =
        provider.CreateProtector("DCenter.Consumables.Supervisor.v2").ToTimeLimitedDataProtector();

    private readonly int sessionHours = Math.Clamp(options.Value.SupervisorSessionHours, 1, 24);

    private readonly int maxSessionHours = Math.Clamp(options.Value.SupervisorMaxSessionHours, 1, 168);

    public int MaxSessionHours => maxSessionHours;

    private long notBeforeMs;

    private readonly ConcurrentDictionary<string, DateTimeOffset> revoked = new(StringComparer.Ordinal);

    // Logout: the token stops working even though it has not expired. Kept in memory until it would have expired;
    // the caller persists the returned entry so the logout survives a restart.
    public RevokedToken? Revoke(string? token)
    {
        var session = Validate(token);
        if (session is null) return null;
        var entry = new RevokedToken(Fingerprint(token!), session.ExpiresAt);
        Restore(entry);
        return entry;
    }

    public void Restore(RevokedToken entry)
    {
        foreach (var (key, expiresAt) in revoked)
            if (expiresAt <= time.GetUtcNow()) revoked.TryRemove(key, out _);
        if (entry.ExpiresAt > time.GetUtcNow()) revoked[entry.Fingerprint] = entry.ExpiresAt;
    }

    private static string Fingerprint(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private const string PayloadVersion = "v3|";

    // loginAt carries the original login through refreshes so a session cannot be renewed forever.
    public (string Token, DateTimeOffset ExpiresAt) Issue(string name, DateTimeOffset? loginAt = null)
    {
        var now = time.GetUtcNow();
        var expiresAt = now.AddHours(sessionHours);
        var login = loginAt ?? now;
        var payload = string.Create(CultureInfo.InvariantCulture,
            $"{PayloadVersion}{now.ToUnixTimeMilliseconds()}|{login.ToUnixTimeMilliseconds()}|{name}");
        return (protector.Protect(payload, expiresAt), expiresAt);
    }

    public bool CanRefresh(SupervisorSession session) => time.GetUtcNow() < session.LoginAt.AddHours(maxSessionHours);

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
            var versioned = payload.StartsWith(PayloadVersion, StringComparison.Ordinal);
            var rest = versioned ? payload[PayloadVersion.Length..] : payload;
            if (!TakeNumber(ref rest, out var issuedMs) || issuedMs < Interlocked.Read(ref notBeforeMs)) return null;
            var loginMs = issuedMs;
            if (versioned && !TakeNumber(ref rest, out loginMs)) return null;
            return new SupervisorSession(rest, expiresAt) { LoginAt = DateTimeOffset.FromUnixTimeMilliseconds(loginMs) };
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static bool TakeNumber(ref string text, out long value)
    {
        value = 0;
        var separator = text.IndexOf('|');
        if (separator <= 0 || !long.TryParse(text.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out value))
            return false;
        text = text[(separator + 1)..];
        return true;
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
