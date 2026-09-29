using DCenter.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DCenter.Server.Tests;

public class SupervisorAuthTests
{
    private static SupervisorAuth Create() =>
        new(new EphemeralDataProtectionProvider(), Options.Create(new ConsumableOptions { SupervisorSessionHours = 12 }), TimeProvider.System);

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

    [Fact]
    public void RevokedTokenStopsWorkingButOthersStayValid()
    {
        var auth = Create();
        var (first, _) = auth.Issue("Supervisor A");
        var (second, _) = auth.Issue("Supervisor B");

        auth.Revoke(first);

        Assert.Null(auth.Validate(first));
        Assert.NotNull(auth.Validate(second));
    }

    [Fact]
    public void RevokingGarbageIsHarmless()
    {
        var auth = Create();
        var (token, _) = auth.Issue("Supervisor A");

        auth.Revoke(null);
        auth.Revoke("not-a-token");

        Assert.NotNull(auth.Validate(token));
    }

    private sealed class MovableTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void TokensFromBeforeTheLoginTimeWasAddedStillValidate()
    {
        var provider = new EphemeralDataProtectionProvider();
        var auth = new SupervisorAuth(provider, Options.Create(new ConsumableOptions()), TimeProvider.System);
        var issued = DateTimeOffset.UtcNow;
        var legacy = provider.CreateProtector("DCenter.Consumables.Supervisor.v2").ToTimeLimitedDataProtector()
            .Protect($"{issued.ToUnixTimeMilliseconds()}|Supervisor A|B", issued.AddHours(1));

        var session = auth.Validate(legacy);

        Assert.NotNull(session);
        Assert.Equal("Supervisor A|B", session.Name);
        Assert.Equal(issued.ToUnixTimeMilliseconds(), session.LoginAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void RefreshKeepsTheOriginalLoginAndStopsAtTheMaximum()
    {
        var clock = new MovableTime(DateTimeOffset.UtcNow);
        var auth = new SupervisorAuth(new EphemeralDataProtectionProvider(),
            Options.Create(new ConsumableOptions { SupervisorSessionHours = 12, SupervisorMaxSessionHours = 24 }), clock);
        var login = clock.Now;
        var (token, _) = auth.Issue("Supervisor A");

        clock.Now = login.AddHours(11);
        var first = auth.Validate(token)!;
        Assert.True(auth.CanRefresh(first));
        var (renewed, _) = auth.Issue(first.Name, first.LoginAt);
        Assert.Equal(login.ToUnixTimeMilliseconds(), auth.Validate(renewed)!.LoginAt.ToUnixTimeMilliseconds());

        clock.Now = login.AddHours(24).AddMinutes(1);
        Assert.False(auth.CanRefresh(new SupervisorSession("Supervisor A", clock.Now.AddHours(1)) { LoginAt = login }));
    }

    [Fact]
    public void RestoredLogoutsAreRejectedAfterARestart()
    {
        var provider = new EphemeralDataProtectionProvider();
        var options = Options.Create(new ConsumableOptions());
        var before = new SupervisorAuth(provider, options, TimeProvider.System);
        var (token, _) = before.Issue("Supervisor A");
        var revoked = before.Revoke(token)!;

        var afterRestart = new SupervisorAuth(provider, options, TimeProvider.System);
        Assert.NotNull(afterRestart.Validate(token));

        afterRestart.Restore(revoked);
        Assert.Null(afterRestart.Validate(token));
    }
}
