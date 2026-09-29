namespace DCenter.Server.Entities;

// A supervisor token that was logged out before it expired; kept so the logout survives an app-pool recycle.
public class SupervisorRevokedToken
{
    public string Fingerprint { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
