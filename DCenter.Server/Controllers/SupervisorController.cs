using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

public record SupervisorLoginRequest(string? Name, string? Password);

public record SupervisorSessionDto(string Name, DateTimeOffset ExpiresAt, string? Token);

public record SupervisorPasswordChange(string? CurrentPassword, string? NewPassword);

[ApiController]
[Route("api/supervisor")]
public class SupervisorController(SupervisorAuth auth, SupervisorPasswordService passwords, ILogger<SupervisorController> logger)
    : ControllerBase
{
    private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(1);

    [HttpPost("login")]
    public async Task<ActionResult<SupervisorSessionDto>> Login(SupervisorLoginRequest request, CancellationToken ct)
    {
        var status = await passwords.StatusAsync(ct);
        if (status.Source == SupervisorPasswordService.SourceNone)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Supervisor login is not set up. Ask IT to set the Consumables__SupervisorPassword environment variable.");

        var name = ConsumableText.FreeText(request.Name, 100);
        if (name is null) return BadRequest("Enter your name so your changes are recorded under it.");

        if (!await passwords.VerifyAsync(request.Password, ct))
        {
            logger.LogWarning("Failed supervisor login for {Name} from {Ip}", name, HttpContext.Connection.RemoteIpAddress);
            await Task.Delay(FailureDelay, ct);
            return Unauthorized("Incorrect supervisor password.");
        }

        var (token, expiresAt) = auth.Issue(name);
        logger.LogInformation("Supervisor login by {Name} from {Ip}", name, HttpContext.Connection.RemoteIpAddress);
        return Ok(new SupervisorSessionDto(name, expiresAt, token));
    }

    [HttpGet("session")]
    public ActionResult<SupervisorSessionDto> Session()
    {
        var session = auth.FromRequest(Request);
        return session is null ? Unauthorized("Supervisor session has expired.") : Ok(new SupervisorSessionDto(session.Name, session.ExpiresAt, null));
    }

    [HttpGet("password")]
    [SupervisorOnly]
    public async Task<ActionResult<SupervisorPasswordStatus>> PasswordStatus(CancellationToken ct)
        => Ok(await passwords.StatusAsync(ct));

    [HttpPost("password")]
    [SupervisorOnly]
    public async Task<ActionResult<SupervisorPasswordStatus>> ChangePassword(SupervisorPasswordChange request, CancellationToken ct)
    {
        var user = auth.FromRequest(Request)!.Name;
        var result = await passwords.ChangeAsync(request.CurrentPassword, request.NewPassword, user, ct);
        if (!result.Succeeded)
        {
            logger.LogWarning("Supervisor password change rejected for {Name}: {Reason}", user, result.Error);
            if (result.Status == StatusCodes.Status403Forbidden) await Task.Delay(FailureDelay, ct);
            return StatusCode(result.Status, result.Error);
        }
        logger.LogInformation("Supervisor password changed by {Name}", user);
        return Ok(result.Value);
    }
}
