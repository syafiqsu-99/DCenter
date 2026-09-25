using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DCenter.Server.Services;

public sealed record SupervisorSession(string Name, DateTimeOffset ExpiresAt);

public class SupervisorAuth(IDataProtectionProvider provider, IOptions<ConsumableOptions> options)
{
    public const string TokenHeader = "X-Supervisor-Token";

    private readonly ITimeLimitedDataProtector protector =
        provider.CreateProtector("DCenter.Consumables.Supervisor.v2").ToTimeLimitedDataProtector();

    private readonly int sessionHours = Math.Clamp(options.Value.SupervisorSessionHours, 1, 24);

    private long notBeforeMs;

    public (string Token, DateTimeOffset ExpiresAt) Issue(string name)
    {
        var now = DateTimeOffset.UtcNow;
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

    public SupervisorSession? FromRequest(HttpRequest request) => Validate(request.Headers[TokenHeader].ToString());
}
