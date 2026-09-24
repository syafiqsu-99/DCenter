namespace DCenter.Server.Entities;

public class SupervisorCredential
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string PasswordHash { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
