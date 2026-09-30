namespace DCenter.Server.Entities;

public class SupervisorCredential
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string PasswordHash { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

// A supervisor token that was logged out before it expired; kept so the logout survives an app-pool recycle.
public class SupervisorRevokedToken
{
    public string Fingerprint { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
