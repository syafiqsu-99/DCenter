using System.Text.Json.Nodes;

namespace DCenter.Server.IntegrationTests;

public sealed class WeldReportScenario : Scenario
{
    protected override string Name => "WeldReport";

    private static object Joint(int number, string welder, string welderNo, string heat, string wps = "WPS-8") => new
    {
        id = 0, jointNumber = number, partDescLeft = "Body", partNoLeft = "BODY-1", heatNumberLeft = heat,
        partDescRight = "Flange", partNoRight = "FLG-2", heatNumberRight = $"{heat}-R",
        wpsNo = wps, rev = "0", welderName = welder, welderNo,
        materials = new[]
        {
            new { id = 0, columnNumber = 1, process = "SMAW", size = "3.2", type = "E7018", manuf = "Kobelco", heatLot = "L-" + heat },
            new { id = 0, columnNumber = 2, process = "GTAW", size = "2.4", type = "ER70S", manuf = "Lincoln", heatLot = "" },
        },
    };

    private static object Report(string wo, string? rowVersion, int id, string? dateWelded, params object[] joints) => new
    {
        id, workOrderNumber = wo, partNo = "ASM-100", description = "Valve body assembly", reportRequired = true,
        dateWelded, materialSpec1 = "SA-516", grade1 = "70", pNumber1 = "1", materialSpec2 = "SA-240", grade2 = "304", pNumber2 = "8",
        rowVersion, joints,
    };

    [SqlFact]
    public async Task WeldReportsBehaveAsRecorded()
    {
        var s = Snap;
        await s.Get("list empty", "/api/reports");
        await s.Get("load missing", "/api/reports/WO100");
        await s.Post("save without date", "/api/reports", Report("WO100", null, 0, null, Joint(1, "Ali", "W-01", "H1")));
        await s.Post("save without work order", "/api/reports", Report("", null, 0, "2026-09-20"));
        await s.Post("save too long work order", "/api/reports", Report(new string('W', 101), null, 0, "2026-09-20"));

        var created = await s.Post("create", "/api/reports", Report("WO100", null, 0, "2026-09-20",
            Joint(1, "Ali", "W-01", "H1"), Joint(2, "Ravi", "W-07", "H2", "WPS-1")));
        var loaded = await s.Get("load", "/api/reports/WO100");
        await s.Post("create again conflicts", "/api/reports", Report("WO100", null, 0, "2026-09-20", Joint(1, "Ali", "W-01", "H1")));

        var rowVersion = loaded!["rowVersion"]!.GetValue<string>();
        var updated = await s.Post("update joints", "/api/reports", Report("WO100", rowVersion, Id(created), "2026-09-21",
            Joint(1, "Ali", "W-01", "H1"), Joint(2, "Ravi", "W-07", "H2B", "WPS-1"), Joint(3, "Ali", "W-01", "H3")));
        await s.Post("update with stale version", "/api/reports", Report("WO100", rowVersion, Id(created), "2026-09-21", Joint(1, "Ali", "W-01", "H1")));
        await s.Get("load after update", "/api/reports/WO100");

        await s.Post("second report", "/api/reports", Report("WO101", null, 0, "2026-08-02", Joint(1, "Ravi", "W-07", "HX9")));
        await s.Post("third report without joints", "/api/reports", Report("WO102", null, 0, "2026-07-15"));

        await s.Post("complete", "/api/reports/WO100/complete", true);
        await s.Post("complete report without joints", "/api/reports/WO102/complete", true);
        await s.Post("complete missing", "/api/reports/WO999/complete", true);
        await s.Post("edit completed", "/api/reports", Report("WO100", updated!["rowVersion"]!.GetValue<string>(), Id(created), "2026-09-21", Joint(1, "Ali", "W-01", "H1")));
        await s.Post("reopen without supervisor", "/api/reports/WO100/complete", false);
        await s.Delete("delete without supervisor", "/api/reports/WO101");

        await LoginSupervisorAsync();
        await s.Delete("delete completed", "/api/reports/WO100");
        await s.Post("reopen", "/api/reports/WO100/complete", false);
        await s.Post("complete again", "/api/reports/WO100/complete", true);
        await s.Get("history", "/api/reports/WO100/history");
        await s.Get("history missing", "/api/reports/WO999/history");

        await s.Get("list", "/api/reports");
        await s.Get("dashboard default", "/api/reports/dashboard");
        await s.Get("dashboard 12 months", "/api/reports/dashboard?months=12");
        await s.Get("trace welder", "/api/reports/trace?field=welder&q=ali");
        await s.Get("trace wps", "/api/reports/trace?field=wps&q=WPS-1");
        await s.Get("trace heat", "/api/reports/trace?field=heat&q=H2");
        await s.Get("trace heat lot", "/api/reports/trace?field=heatLot&q=L-H3");
        await s.Get("trace bad field", "/api/reports/trace?field=colour&q=red");
        await s.Get("trace short", "/api/reports/trace?field=welder&q=a");

        await s.Get("signoff defaults", "/api/reports/WO100/signoff");
        await s.Get("signoff missing", "/api/reports/WO999/signoff");
        await s.Get("excel defaults", "/api/reports/WO100/excel");
        await s.Post("excel with signoff", "/api/reports/WO100/excel", new
        {
            engineerName = "Siti Eng", engineerDate = "2026-10-01", qaName = "Lim QA", qaDate = "2026-10-02",
            welders = new[] { new { welderName = "Ali", welderNo = "W-01", displayName = "Ali Bin Abu (ID W-01)", date = "2026-09-22" } },
        });
        await s.Get("pdf defaults", "/api/reports/WO100/pdf");
        await s.Post("pdf with signoff", "/api/reports/WO100/pdf", new { engineerName = "Siti Eng", qaName = "Lim QA", welders = Array.Empty<object>() });
        await s.Get("excel missing", "/api/reports/WO999/excel");

        await s.Delete("delete draft", "/api/reports/WO101");
        await s.Delete("delete missing", "/api/reports/WO101");
        await s.Get("final list", "/api/reports");

        s.Verify();
    }
}
