using DCenter.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

public abstract class SettingsControllerBase : ControllerBase
{
    protected ActionResult<T> ToAction<T>(ServiceResult<T> result)
        => result.Succeeded ? Ok(result.Value) : Failure(result);

    protected IActionResult ToNoContent<T>(ServiceResult<T> result)
        => result.Succeeded ? NoContent() : Failure(result);

    // An empty error (ServiceResult.NotFound) becomes a bare status code, which [ApiController] turns into the
    // same problem response that NotFound() produces.
    private ActionResult Failure<T>(ServiceResult<T> result)
        => string.IsNullOrEmpty(result.Error) ? StatusCode(result.Status) : StatusCode(result.Status, result.Error);
}
