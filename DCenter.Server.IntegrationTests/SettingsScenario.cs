namespace DCenter.Server.IntegrationTests;

public sealed class SettingsScenario : Scenario
{
    protected override string Name => "Settings";

    [SqlFact]
    public async Task SettingsBehaveAsRecorded()
    {
        var s = Snap;
        await s.Post("welder create without supervisor", "/api/welders", new { welderName = "Nobody", welderNo = "X" });
        await LoginSupervisorAsync();

        var ali = await s.Post("welder create", "/api/welders", new { welderName = "Ali Bin Abu", welderNo = "W-01", isActive = true, usageScope = "ReportAndStock" });
        var ravi = await s.Post("welder create 2", "/api/welders", new { welderName = "Ravi Kumar", welderNo = "W-07", isActive = true, usageScope = "Report" });
        await s.Post("welder create inactive", "/api/welders", new { welderName = "Zul Old", welderNo = "W-09", isActive = false });
        await s.Post("welder duplicate number", "/api/welders", new { welderName = "Other", welderNo = "W-01" });
        await s.Post("welder invalid scope", "/api/welders", new { welderName = "Scope Bad", welderNo = "W-10", usageScope = "Nope" });
        await s.Post("welder name too long", "/api/welders", new { welderName = new string('n', 201), welderNo = "W-11" });
        await s.Put("welder update", $"/api/welders/{Id(ravi)}", new { welderName = "Ravi Kumar A/L Raju", welderNo = "W-07", isActive = true, usageScope = "Report" });
        await s.Put("welder update to taken number", $"/api/welders/{Id(ravi)}", new { welderName = "Ravi", welderNo = "W-01", isActive = true });
        await s.Put("welder update missing", "/api/welders/9999", new { welderName = "Ghost", welderNo = "G" });
        await s.Put("welder scope bulk", "/api/welders/scope", new { ids = new[] { Id(ali), Id(ravi) }, usageScope = "ReportAndStock" });
        await s.Put("welder scope empty", "/api/welders/scope", new { ids = Array.Empty<int>(), usageScope = "Report" });
        await s.Get("welders list", "/api/welders");
        await s.Get("welders search all", "/api/welders/search");
        await s.Get("welders search q", "/api/welders/search?q=rav");
        await s.Get("welders search stock only", "/api/welders/search?stockOnly=true");
        var temp = await s.Post("welder create temp", "/api/welders", new { welderName = "Temp Person", welderNo = "" });
        await s.Delete("welder delete", $"/api/welders/{Id(temp)}");
        await s.Delete("welder delete missing", "/api/welders/9999");

        var process = await s.Post("lookup create", "/api/lookups", new { category = "Process", value = "SMAW", sortOrder = 0, isActive = true });
        await s.Post("lookup create 2", "/api/lookups", new { category = "Process", value = "GTAW", sortOrder = 1, isActive = true });
        await s.Post("lookup create size", "/api/lookups", new { category = "Size", value = "3.20", sortOrder = 0, isActive = true });
        await s.Post("lookup duplicate", "/api/lookups", new { category = "Process", value = "SMAW", sortOrder = 5, isActive = true });
        await s.Post("lookup bad category", "/api/lookups", new { category = "Colour", value = "Red", sortOrder = 0, isActive = true });
        await s.Post("lookup too long", "/api/lookups", new { category = "Type", value = new string('t', 201), sortOrder = 0, isActive = true });
        var gmaw = await s.Post("lookup create 3", "/api/lookups", new { category = "Process", value = "GMAW", sortOrder = 2, isActive = false });
        await s.Put("lookup update", $"/api/lookups/{Id(gmaw)}", new { category = "Process", value = "GMAW-P", sortOrder = 2, isActive = true });
        await s.Put("lookup update to duplicate", $"/api/lookups/{Id(gmaw)}", new { category = "Process", value = "GTAW", sortOrder = 2, isActive = true });
        await s.Put("lookup reorder", "/api/lookups/reorder", new[] { Id(gmaw), Id(process) });
        await s.Get("lookups all", "/api/lookups");
        await s.Get("lookups process", "/api/lookups?category=Process");
        await s.Get("lookups export", "/api/lookups/export");
        await s.Upload("lookups import", "/api/lookups/import", "lists.csv",
            "Category,Value,SortOrder,IsActive\nProcess,smaw,7,1\nType,E7018,1,yes\nType,E308L,x,no\nColour,Red,1,1\n,Blank,1,1\n");
        await s.Upload("lookups import bad header", "/api/lookups/import", "lists.csv", "Name,Thing\nA,B\n");
        await s.Get("lookups after import", "/api/lookups");
        await s.Delete("lookup delete", $"/api/lookups/{Id(gmaw)}");
        await s.Delete("lookup delete missing", "/api/lookups/9999");

        var link = await s.Post("link create", "/api/processtypelinks", new { process = "SMAW", type = "E7018" });
        await s.Post("link duplicate", "/api/processtypelinks", new { process = "SMAW", type = "E7018" });
        await s.Post("link missing type", "/api/processtypelinks", new { process = "SMAW", type = "" });
        await s.Post("link create 2", "/api/processtypelinks", new { process = "GTAW", type = "ER308L" });
        await s.Get("links list", "/api/processtypelinks");
        await s.Delete("link delete", $"/api/processtypelinks/{Id(link)}");
        await s.Delete("link delete missing", "/api/processtypelinks/9999");

        var wps = await s.Post("wps create", "/api/wps", new { wpsNo = "WPS-8", baseMetal = "SS", process = "SMAW", pNo = "8" });
        await s.Post("wps create 2", "/api/wps", new { wpsNo = "WPS-1", baseMetal = "CS", process = "GTAW", pNo = "1" });
        await s.Post("wps duplicate key case", "/api/wps", new { wpsNo = "wps-8", baseMetal = "X", process = "Y", pNo = "8" });
        await s.Post("wps missing pno", "/api/wps", new { wpsNo = "WPS-9", pNo = "" });
        await s.Post("wps too long", "/api/wps", new { wpsNo = "WPS-10", pNo = new string('9', 51) });
        await s.Put("wps update", $"/api/wps/{Id(wps)}", new { wpsNo = "WPS-8", baseMetal = "Stainless", process = "SMAW", pNo = "8" });
        await s.Put("wps update missing", "/api/wps/9999", new { wpsNo = "Z", pNo = "1" });
        await s.Get("wps list", "/api/wps");
        await s.Get("wps export", "/api/wps/export");
        await s.Upload("wps import", "/api/wps/import", "wps.csv",
            "WPS No.,P-No.,Base Metal,Process\nWPS-8,8,Stainless,GTAW\nWPS-2,1,CS,SMAW\nWPS-1,1,CS,GTAW\n,1,x,y\n");
        await s.Upload("wps import not csv", "/api/wps/import", "wps.xlsx", "WpsNo,PNo\nA,1\n");
        await s.Get("wps after import", "/api/wps");

        var mrn = await s.Post("mrn create", "/api/mrn", new { mrn = "MRN-1", specNo = "SA-516", form = "Plate", fullSpecification = "ASME SA-516 Gr 70" });
        await s.Post("mrn duplicate", "/api/mrn", new { mrn = "mrn-1", specNo = "sa-516" });
        await s.Put("mrn update", $"/api/mrn/{Id(mrn)}", new { mrn = "MRN-1", specNo = "SA-516", form = "Plate", fullSpecification = "SA-516-70" });
        await s.Get("mrn export", "/api/mrn/export");
        await s.Upload("mrn import legacy", "/api/mrn/import", "mrn.csv",
            "MRN,Form,FullSpecification,SpecNoRaw,SpecNo\nMRN-1,Sheet,SA-516-70,SA-516,SA516\nMRN-2,Pipe,SA-106 B,,SA106\n");
        await s.Get("mrn list", "/api/mrn");
        await s.Delete("mrn delete", $"/api/mrn/{Id(mrn)}");

        var bpvc = await s.Post("bpvc create", "/api/bpvc", new { specNo = "SA-516", designation = "70", unsNo = "K02700", pNo = "1", groupNo = "2", nominalComposition = "C-Mn-Si" });
        await s.Post("bpvc create 2", "/api/bpvc", new { specNo = "SA-240", designation = "304", unsNo = "S30400", pNo = "8", groupNo = "1" });
        await s.Post("bpvc duplicate", "/api/bpvc", new { specNo = "sa-516", designation = "70", unsNo = "k02700", pNo = "1" });
        await s.Put("bpvc update", $"/api/bpvc/{Id(bpvc)}", new { specNo = "SA-516", designation = "70", unsNo = "K02700", pNo = "1", groupNo = "2", minTensile = "70", nominalComposition = "C-Mn-Si" });
        await s.Get("bpvc export", "/api/bpvc/export");
        await s.Upload("bpvc import", "/api/bpvc/import", "bpvc.csv",
            "SpecNo,Designation,UnsNo,PNo,MinTensile,GroupNo\nSA-516,70,K02700,1,70,2\nSA-516,60,K02100,1,60,1\n=SA-1,1,,1,,\n");
        await s.Get("bpvc list", "/api/bpvc");
        await s.Delete("bpvc delete", $"/api/bpvc/{Id(bpvc)}");
        await s.Delete("bpvc delete missing", "/api/bpvc/9999");
        await s.Get("bpvc final", "/api/bpvc");

        s.Verify();
    }
}
