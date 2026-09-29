using System.Text.Json.Nodes;

namespace DCenter.Server.IntegrationTests;

public sealed class ConsumablesScenario : Scenario
{
    protected override string Name => "Consumables";

    private const string Electrode = "Electrode Filler";
    private const string Bare = "Bare & Powder Filler";
    private const string Day = "2026-09-29";

    private static int LotId(JsonNode? movement) => movement!["lines"]![0]!["lotId"]!.GetValue<int>();

    [SqlFact]
    public async Task ConsumablesBehaveAsRecorded()
    {
        var s = Snap;
        await s.Get("catalog", "/api/consumables/catalog");
        await s.Get("ovens empty", "/api/consumables/ovens");
        await s.Post("receive without supervisor", "/api/consumables/receive", new { quantityKg = 1 });

        await LoginSupervisorAsync("Store Supervisor");
        var ali = await s.Post("welder for stock", "/api/welders", new { welderName = "Ali Bin Abu", welderNo = "W-01", isActive = true, usageScope = "ReportAndStock" });
        var aliId = Id(ali);

        var e7018 = await s.Post("item electrode", "/api/consumables/items", new
        { category = Electrode, specification = "e7018", diameter = "3.2", minStockKg = 5, activatedMinKg = 2, finishThresholdKg = 0.3, isActive = true, holdingOvenType = "Mild Steel" });
        var er308 = await s.Post("item bare filler", "/api/consumables/items", new
        { category = Bare, specification = "ER308L", diameter = "2.4", minStockKg = 3, activatedMinKg = 1, isActive = true });
        await s.Post("item duplicate", "/api/consumables/items", new { category = Electrode, specification = "E7018", diameter = "3.20", isActive = true, holdingOvenType = "Mild Steel" });
        await s.Post("item bad category", "/api/consumables/items", new { category = "Gas", specification = "Argon", diameter = "1", isActive = true });
        var spare = await s.Post("item spare", "/api/consumables/items", new { category = Bare, specification = "ER70S-6", diameter = "1.2", isActive = true });
        await s.Put("item update", $"/api/consumables/items/{Id(spare)}", new { category = Bare, specification = "ER70S-6", diameter = "1.2", minStockKg = 2, isActive = true });
        await s.Get("items", "/api/consumables/items");
        await s.Get("items search", "/api/consumables/items?q=7018&category=" + Uri.EscapeDataString(Electrode));

        var r1 = await s.Post("receive electrode", "/api/consumables/receive", new
        { txnDate = Day, source = "Weld Shop", receivedByWelderId = aliId, itemId = Id(e7018), brand = "kobelco", lotNumber = "l123", quantityKg = 20, remarks = "first" });
        var r2 = await s.Post("receive bare filler", "/api/consumables/receive", new
        { txnDate = Day, source = "Tool Crib", receivedByWelderId = aliId, itemId = Id(er308), brand = "Lincoln", lotNumber = "B77", quantityKg = 10 });
        await s.Post("receive new item", "/api/consumables/receive", new
        { txnDate = Day, source = "Weld Shop", receivedByWelderId = aliId, newItem = new { category = Bare, specification = "ERNiCr-3", diameter = "2.4", isActive = true }, brand = "Special", lotNumber = "N1", quantityKg = 4 });
        await s.Post("receive future date", "/api/consumables/receive", new { txnDate = "2026-10-05", source = "Weld Shop", receivedByWelderId = aliId, itemId = Id(e7018), brand = "K", lotNumber = "L1", quantityKg = 1 });
        await s.Post("receive zero", "/api/consumables/receive", new { txnDate = Day, source = "Weld Shop", receivedByWelderId = aliId, itemId = Id(e7018), brand = "K", lotNumber = "L1", quantityKg = 0 });
        var electrodeLot = LotId(r1);
        var bareLot = LotId(r2);

        await s.Post("transfer bare to activated", "/api/consumables/transfer", new { txnDate = Day, itemId = Id(er308), lotId = bareLot, fromStage = "Normal", toStage = "Activated", quantityKg = 3 });
        await s.Post("transfer electrode refused", "/api/consumables/transfer", new { txnDate = Day, itemId = Id(e7018), fromStage = "Normal", toStage = "Activated", quantityKg = 1 });
        await s.Post("transfer too much", "/api/consumables/transfer", new { txnDate = Day, itemId = Id(er308), fromStage = "Normal", toStage = "Activated", quantityKg = 99 });

        var bake = await s.Post("send to bake", "/api/consumables/baking", new { bakingDate = Day, itemId = Id(e7018), lotId = electrodeLot, quantityKg = 6, personInCharge = "pic one" });
        var bakeId = bake!["records"]![0]!["id"]!.GetValue<int>();
        await s.Post("send to bake bare refused", "/api/consumables/baking", new { bakingDate = Day, itemId = Id(er308), quantityKg = 1, personInCharge = "pic one" });
        await s.Get("baking board", "/api/consumables/baking/board");
        await s.Post("bake start", "/api/consumables/baking/start", new { ids = new[] { bakeId }, at = "2026-09-29T09:00:00" });
        await s.Post("bake stop", "/api/consumables/baking/stop", new { ids = new[] { bakeId }, at = "2026-09-29T10:00:00" });
        await s.Put("baking update", $"/api/consumables/baking/{bakeId}", new { personInCharge = "PIC One", bakingDate = Day, bakeStart = "2026-09-29T09:00:00", bakeStop = "2026-09-29T10:05:00", remarks = "edited" });
        await s.Put("baking update bad times", $"/api/consumables/baking/{bakeId}", new { personInCharge = "PIC One", bakingDate = Day, bakeStart = "2026-09-29T11:00:00", bakeStop = "2026-09-29T10:00:00" });

        await s.Post("place wrong oven", "/api/consumables/holding", new { holdingDate = Day, bakingRecordId = bakeId, compartmentId = 1, quantityKg = 2 });
        await s.Post("place into MS-1", "/api/consumables/holding", new { holdingDate = Day, bakingRecordId = bakeId, compartmentId = 10, quantityKg = 4 });
        await s.Post("place rest into MS-2", "/api/consumables/holding", new { holdingDate = Day, bakingRecordId = bakeId, compartmentId = 11, takeAll = true });
        await s.Get("ovens", "/api/consumables/ovens");

        await s.Post("move MS-2 to MS-1", "/api/consumables/move", new { txnDate = Day, itemId = Id(e7018), fromCompartmentId = 11, toCompartmentId = 10, quantityKg = 1 });
        await s.Post("move to wrong oven", "/api/consumables/move", new { txnDate = Day, itemId = Id(e7018), fromCompartmentId = 10, toCompartmentId = 2, quantityKg = 1 });

        var issue = await s.Post("issue electrode", "/api/consumables/issue", new { txnDate = Day, welderId = aliId, itemId = Id(e7018), compartmentId = 10, quantityKg = 1.5 });
        await s.Post("issue bare filler", "/api/consumables/issue", new { txnDate = Day, welderId = aliId, itemId = Id(er308), quantityKg = 1 });
        await s.Post("issue too much", "/api/consumables/issue", new { txnDate = Day, welderId = aliId, itemId = Id(er308), quantityKg = 50 });
        await s.Post("return electrode to oven", "/api/consumables/return", new { txnDate = Day, welderId = aliId, itemId = Id(e7018), compartmentId = 10, quantityKg = 0.5 });
        await s.Post("return more than issued", "/api/consumables/return", new { txnDate = Day, welderId = aliId, itemId = Id(er308), quantityKg = 9 });
        await s.Get("counter", $"/api/consumables/counter?welderId={aliId}");
        await s.Get("counter today", $"/api/consumables/counter/welders/{aliId}/today");

        await s.Post("adjust activated bare", "/api/consumables/adjust", new { txnDate = Day, itemId = Id(er308), stage = "Activated", countedQtyKg = 1.5, reason = "Count variance" });
        await s.Post("adjust normal refused", "/api/consumables/adjust", new { txnDate = Day, itemId = Id(er308), stage = "Normal", countedQtyKg = 1, reason = "Count variance" });
        await s.Post("adjust other without remarks", "/api/consumables/adjust", new { txnDate = Day, itemId = Id(er308), stage = "Activated", countedQtyKg = 1, reason = "Other" });
        await s.Post("finish bare activated", "/api/consumables/finish", new { itemId = Id(er308), stage = "Activated", reason = "Used up" });

        await s.Post("void issue", $"/api/consumables/transactions/{issue!["txnNo"]!.GetValue<string>()}/void", new { remarks = "wrong welder" });
        await s.Post("void again", $"/api/consumables/transactions/{issue["txnNo"]!.GetValue<string>()}/void", new { remarks = "again" });
        await s.Post("void missing", "/api/consumables/transactions/CT-26-999999/void", new { remarks = "x" });

        await s.Get("balances", "/api/consumables/balances");
        await s.Get("balances include zero", "/api/consumables/balances?includeZero=true");
        await s.Get("lot stock", "/api/consumables/lot-stock");
        await s.Get("normal stock electrode", "/api/consumables/normal-stock?category=" + Uri.EscapeDataString(Electrode));
        await s.Get("lots", $"/api/consumables/items/{Id(e7018)}/lots");
        await s.Get("lot balances", $"/api/consumables/items/{Id(e7018)}/lot-balances?includeZero=true");
        await s.Get("recent quantities", $"/api/consumables/items/{Id(e7018)}/recent-quantities?type=Issue");
        await s.Get("transactions", "/api/consumables/transactions");
        await s.Get("transactions filtered", $"/api/consumables/transactions?Type=Issue&WelderId={aliId}");
        await s.Get("transactions for baking", $"/api/consumables/transactions?BakingRecordId={bakeId}");
        await s.Get("transactions today", "/api/consumables/transactions/today?type=Receive");
        await s.Get("baking list", "/api/consumables/baking");
        await s.Get("holding list", "/api/consumables/holding");
        await s.Get("dashboard", "/api/consumables/dashboard");
        await s.Get("consumption", "/api/consumables/dashboard/consumption?month=2026-09");

        var sheet = await s.Get("count sheet activated", "/api/consumables/count-sheet?scope=Activated");
        var lines = sheet!["lines"]!.AsArray().Select(l => new
        {
            itemId = l!["itemId"]!.GetValue<int>(), lotId = l["lotId"]!.GetValue<int>(),
            compartmentId = l["compartmentId"]?.GetValue<int?>(), systemKg = l["systemKg"]!.GetValue<decimal>(),
            countedKg = l["systemKg"]!.GetValue<decimal>() - 0.2m,
        }).ToList();
        var count = await s.Post("stock count", "/api/consumables/stock-counts", new { countDate = Day, scope = "Activated", remarks = "monthly", lines });
        await s.Get("stock counts", "/api/consumables/stock-counts");
        if (count?["referenceNo"] is JsonNode reference)
            await s.Get("stock count detail", $"/api/consumables/stock-counts/{reference.GetValue<string>()}");
        await s.Get("count sheet normal", "/api/consumables/count-sheet?scope=Normal");

        await s.Get("items export", "/api/consumables/items/export");
        await s.Get("items template", "/api/consumables/items/export?template=true");
        const string itemsCsv = "Type,Specification,Diameter,Min Stock (KG),Activated Min (KG),Finish Threshold (KG),Holding Oven Type,Active\n"
            + "Electrode Filler,E7018,3.2,8,2,0.3,Mild Steel,Yes\nBare & Powder Filler,ER316L,1.6,1,,,,Yes\nGas,Argon,1,,,,,\n";
        await s.Upload("items import preview", "/api/consumables/items/import", "items.csv", itemsCsv);
        await s.Upload("items import commit", "/api/consumables/items/import?commit=true&skipInvalid=true", "items.csv", itemsCsv);
        await s.Get("stock import template", "/api/consumables/stock-import/template");
        const string stockCsv = "Date,Type,Specification,Diameter,Brand,Lot / Heat No.,Quantity (KG),Storage,Compartment,Holding Oven,Source,Remarks\n"
            + "2026-09-28,Bare & Powder Filler,ER316L,1.6,Lincoln,S1,2.5,Normal,,,Weld Shop,opening\n"
            + "2026-09-28,Electrode Filler,E7018,3.2,Kobelco,L999,1.0,Activated,MS-3,,Weld Shop,\n"
            + "bad-date,Bare & Powder Filler,ER316L,1.6,Lincoln,S2,x,Normal,,,Weld Shop,\n";
        await s.Upload("stock import preview", "/api/consumables/stock-import", "stock.csv", stockCsv);
        await s.Upload("stock import commit", "/api/consumables/stock-import?commit=true&skipInvalid=true", "stock.csv", stockCsv);
        await s.Upload("stock import again detects duplicates", "/api/consumables/stock-import", "stock.csv", stockCsv);

        await s.Delete("delete used item", $"/api/consumables/items/{Id(e7018)}");
        await s.Delete("delete spare item", $"/api/consumables/items/{Id(spare)}");
        await s.Delete("delete missing item", "/api/consumables/items/9999");

        await s.Get("balances after imports", "/api/consumables/balances?includeZero=true");
        await s.Get("transactions after imports", "/api/consumables/transactions?Take=200");

        Snap.Client.DefaultRequestHeaders.Remove("X-Supervisor-Token");
        EnteredBy("Ali Bin Abu");
        await s.Post("welder issue backdated too far", "/api/consumables/issue", new { txnDate = "2026-09-01", welderId = aliId, itemId = Id(e7018), compartmentId = 10, quantityKg = 0.5 });
        await s.Post("welder issue", "/api/consumables/issue", new { txnDate = Day, welderId = aliId, itemId = Id(e7018), compartmentId = 10, quantityKg = 0.5 });
        await s.Post("welder return", "/api/consumables/return", new { txnDate = Day, welderId = aliId, itemId = Id(e7018), compartmentId = 10, quantityKg = 0.2 });
        await s.Post("welder transfer refused", "/api/consumables/transfer", new { txnDate = Day, itemId = Id(er308), fromStage = "Normal", toStage = "Activated", quantityKg = 1 });
        await s.Get("final ovens", "/api/consumables/ovens");

        s.Verify();
    }
}
