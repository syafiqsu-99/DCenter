namespace DCenter.Server.Services;

public interface ISupervisorContext
{
    bool IsSupervisor { get; }
}

public sealed class HttpSupervisorContext(IHttpContextAccessor http, SupervisorAuth auth) : ISupervisorContext
{
    public bool IsSupervisor => http.HttpContext?.Request is { } request && auth.FromRequest(request) is not null;
}
