namespace DCenter.Server.IntegrationTests;

public sealed class WorkOrderAndSupervisorScenario : Scenario
{
    protected override string Name => "WorkOrderAndSupervisor";

    [SqlFact]
    public async Task WorkOrdersAndSupervisorBehaveAsRecorded()
    {
        var s = Snap;
        await s.Get("search all", "/api/workorders/search");
        await s.Get("search prefix", "/api/workorders/search?q=WO10");
        await s.Get("search paged", "/api/workorders/search?q=WO&skip=1&take=2");
        await s.Get("search none", "/api/workorders/search?q=ZZZ");
        await s.Get("header", "/api/workorders/WO100/header");
        await s.Get("header missing", "/api/workorders/WO999/header");
        await s.Post("bom children", "/api/workorders/bom/children", new { items = new[] { "ASM-100", "BODY-1" } });
        await s.Post("bom children empty", "/api/workorders/bom/children", new { items = Array.Empty<string>() });

        await s.Get("session without token", "/api/supervisor/session");
        await s.Post("login wrong password", "/api/supervisor/login", new { name = "QA Lead", password = "wrong" });
        await s.Post("login without name", "/api/supervisor/login", new { name = "", password = ApiFactory.SupervisorPassword });
        await LoginSupervisorAsync();
        await s.Get("session", "/api/supervisor/session");
        await s.Post("refresh", "/api/supervisor/session/refresh");
        await s.Get("password status", "/api/supervisor/password");
        await s.Post("password change wrong current", "/api/supervisor/password", new { currentPassword = "nope", newPassword = "New-Password-22" });
        await s.Post("password change too short", "/api/supervisor/password", new { currentPassword = ApiFactory.SupervisorPassword, newPassword = "short" });
        await s.Post("password change", "/api/supervisor/password", new { currentPassword = ApiFactory.SupervisorPassword, newPassword = "New-Password-22" });
        await s.Get("session after password change", "/api/supervisor/session");
        await s.Get("password status after change", "/api/supervisor/password");
        await s.Post("login new password", "/api/supervisor/login", new { name = "QA Lead", password = "New-Password-22" });
        await s.Post("logout", "/api/supervisor/logout");
        await s.Get("session after logout", "/api/supervisor/session");

        s.Verify();
    }
}
