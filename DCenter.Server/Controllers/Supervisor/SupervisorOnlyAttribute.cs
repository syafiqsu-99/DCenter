using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DCenter.Server.Controllers;

public sealed class SupervisorOnlyAttribute() : TypeFilterAttribute(typeof(SupervisorOnlyFilter));

public sealed class SupervisorOnlyFilter(SupervisorAuth auth) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (auth.FromRequest(context.HttpContext.Request) is not null) return;
        context.Result = new ObjectResult("Supervisor login is required. Use the Login button at the top of the page and try again.")
        {
            StatusCode = StatusCodes.Status401Unauthorized,
        };
    }
}
