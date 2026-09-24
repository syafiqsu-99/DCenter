using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

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
        try
        {
            return ToAction(await action());
        }
        catch (TimeoutException ex)
        {
            return Conflict(ex.Message);
        }
    }

    protected ActionResult<T> ToAction<T>(ServiceResult<T> result)
        => result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, result.Error);
}
