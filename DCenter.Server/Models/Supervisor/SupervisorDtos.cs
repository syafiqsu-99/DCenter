namespace DCenter.Server.Models;

public record SupervisorLoginRequest(string? Name, string? Password);

public record SupervisorSessionDto(string Name, DateTimeOffset ExpiresAt, string? Token);

public record SupervisorPasswordChange(string? CurrentPassword, string? NewPassword);
