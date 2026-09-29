using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

public abstract class ConsumableControllerBase : ControllerBase
{
    private const string EnteredByHeader = "X-Entered-By";

    protected string? EnteredBy
    {
        get
        {
            var supervisor = HttpContext.RequestServices.GetRequiredService<SupervisorAuth>().FromRequest(Request);
            if (supervisor is not null) return supervisor.Name;

            var raw = Request.Headers[EnteredByHeader].ToString();
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                return Uri.UnescapeDataString(raw);
            }
            catch (UriFormatException)
            {
                return raw;
            }
        }
    }

    protected bool IsSupervisor
        => HttpContext.RequestServices.GetRequiredService<SupervisorAuth>().FromRequest(Request) is not null;

    protected async Task<ActionResult<T>> Locked<T>(Func<Task<ServiceResult<T>>> action)
    {
        var gate = HttpContext.RequestServices.GetRequiredService<IdempotencyGate>();
        var scope = $"{Request.Method} {Request.Path}{Request.QueryString}|{EnteredBy}";
        try
        {
            return ToAction(await gate.RunAsync(scope, Request.Headers[IdempotencyGate.Header].ToString(), action));
        }
        catch (TimeoutException ex)
        {
            return Conflict(ex.Message);
        }
        catch (DbUpdateException ex)
        {
            return SaveFailed(ex, scope);
        }
    }

    protected ObjectResult SaveFailed(DbUpdateException ex, string scope)
    {
        HttpContext.RequestServices.GetRequiredService<ILogger<ConsumableControllerBase>>()
            .LogWarning(ex, "Database update failed for {Scope}", scope);
        return ReportSaveRules.IsTruncation(ex)
            ? BadRequest("One of the values is longer than the system allows, so nothing was saved. Shorten it and try again.")
            : Conflict("Someone else saved a change at the same moment, so this was not recorded. Please try again.");
    }

    protected ActionResult<T> ToAction<T>(ServiceResult<T> result)
        => result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, result.Error);
}
