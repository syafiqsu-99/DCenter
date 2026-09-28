using DCenter.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DCenter.Server.Tests;

public class SupervisorAuthTests
{
    private static SupervisorAuth Create() =>
        new(new EphemeralDataProtectionProvider(), Options.Create(new ConsumableOptions { SupervisorSessionHours = 12 }));

    [Fact]
    public void IssuedTokenValidatesWithTheSameName()
    {
        var auth = Create();
        var (token, expiresAt) = auth.Issue("Supervisor A");

        var session = auth.Validate(token);

        Assert.NotNull(session);
        Assert.Equal("Supervisor A", session.Name);
        Assert.True(expiresAt > DateTimeOffset.UtcNow.AddHours(11));
    }

    [Fact]
    public void TamperedOrEmptyTokensAreRejected()
    {
        var auth = Create();
        var (token, _) = auth.Issue("Supervisor A");

        Assert.Null(auth.Validate(token[..^4] + "AAAA"));
        Assert.Null(auth.Validate(""));
        Assert.Null(auth.Validate(null));
    }

    [Fact]
    public void TokensFromAnotherKeyRingAreRejected()
    {
        var (token, _) = Create().Issue("Supervisor A");

        Assert.Null(Create().Validate(token));
    }

    [Fact]
    public void RevokeInvalidatesTokensIssuedEarlier()
    {
        var auth = Create();
        var (old, _) = auth.Issue("Supervisor A");

        auth.RevokeIssuedBefore(DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Null(auth.Validate(old));
    }
}
