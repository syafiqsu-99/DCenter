using System.Security.Cryptography;
using System.Text;
using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DCenter.Server.Services;

public record SupervisorPasswordStatus(string Source, DateTime? UpdatedAt, string? UpdatedBy);

public class SupervisorPasswordService(WeldReportContext db, IOptions<ConsumableOptions> options)
{
    public const string SourceDatabase = "Database";
    public const string SourceEnvironment = "Environment";
    public const string SourceNone = "None";

    private const int MinLength = 8;
    private const int MaxLength = 128;
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly string? environmentPassword = options.Value.SupervisorPassword;

    public async Task<SupervisorPasswordStatus> StatusAsync(CancellationToken ct)
    {
        var row = await db.SupervisorCredentials.AsNoTracking()
            .Where(c => c.Id == SupervisorCredential.SingletonId)
            .Select(c => new { c.UpdatedAt, c.UpdatedBy })
            .FirstOrDefaultAsync(ct);
        if (row is not null) return new SupervisorPasswordStatus(SourceDatabase, row.UpdatedAt, row.UpdatedBy);
        return new SupervisorPasswordStatus(string.IsNullOrEmpty(environmentPassword) ? SourceNone : SourceEnvironment, null, null);
    }

    public async Task<bool> VerifyAsync(string? password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password)) return false;

        var hash = await db.SupervisorCredentials.AsNoTracking()
            .Where(c => c.Id == SupervisorCredential.SingletonId)
            .Select(c => c.PasswordHash)
            .FirstOrDefaultAsync(ct);
        if (hash is not null) return VerifyHash(password, hash);

        if (string.IsNullOrEmpty(environmentPassword)) return false;
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(environmentPassword)),
            SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }

    public async Task<ServiceResult<SupervisorPasswordStatus>> ChangeAsync(
        string? currentPassword, string? newPassword, string user, CancellationToken ct)
    {
        if (!await VerifyAsync(currentPassword, ct))
            return ServiceResult<SupervisorPasswordStatus>.Fail("The current password is incorrect.", StatusCodes.Status403Forbidden);

        var next = newPassword ?? string.Empty;
        if (next.Length < MinLength || next.Length > MaxLength)
            return ServiceResult<SupervisorPasswordStatus>.Fail($"The new password must be {MinLength} to {MaxLength} characters.");
        if (next.Trim().Length != next.Length)
            return ServiceResult<SupervisorPasswordStatus>.Fail("The new password cannot start or end with a space.");
        if (next == currentPassword)
            return ServiceResult<SupervisorPasswordStatus>.Fail("The new password must be different from the current one.");

        var row = await db.SupervisorCredentials.FirstOrDefaultAsync(c => c.Id == SupervisorCredential.SingletonId, ct);
        if (row is null)
        {
            row = new SupervisorCredential();
            db.SupervisorCredentials.Add(row);
        }
        row.PasswordHash = Hash(next);
        row.UpdatedBy = user;
        row.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(ct);

        return ServiceResult<SupervisorPasswordStatus>.Ok(new SupervisorPasswordStatus(SourceDatabase, row.UpdatedAt, row.UpdatedBy));
    }

    private static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"PBKDF2-SHA256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyHash(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256" || !int.TryParse(parts[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
