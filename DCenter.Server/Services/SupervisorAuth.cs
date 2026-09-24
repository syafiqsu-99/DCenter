using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DCenter.Server.Services;

public sealed record SupervisorSession(string Name, DateTimeOffset ExpiresAt);

public class SupervisorAuth(IDataProtectionProvider provider, IOptions<ConsumableOptions> options)
{
    public const string TokenHeader = "X-Supervisor-Token";

    private readonly ITimeLimitedDataProtector protector =
        provider.CreateProtector("DCenter.Consumables.Supervisor.v1").ToTimeLimitedDataProtector();

    private readonly int sessionHours = Math.Clamp(options.Value.SupervisorSessionHours, 1, 24);

    public (string Token, DateTimeOffset ExpiresAt) Issue(string name)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(sessionHours);
        return (protector.Protect(name, expiresAt), expiresAt);
    }

    public SupervisorSession? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var name = protector.Unprotect(token, out var expiresAt);
            return new SupervisorSession(name, expiresAt);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public SupervisorSession? FromRequest(HttpRequest request) => Validate(request.Headers[TokenHeader].ToString());
}
