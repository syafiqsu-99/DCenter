using DCenter.Server.Data;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Cat = DCenter.Server.Entities.StockCatalog;

namespace DCenter.Server.Services;

public class OvenService(WeldReportContext db, ConsumableLedger ledger)
{
    public async Task<OvenBoardDto> GetBoardAsync(CancellationToken ct)
    {
        var ovens = await db.Ovens.AsNoTracking()
            .OrderBy(o => o.Id)
            .Select(o => new
            {
                o.Id, o.Name, o.Code, o.OvenType,
                Compartments = o.Compartments.OrderBy(c => c.Number).Select(c => new { c.Id, c.Number, c.Label }).ToList(),
            })
            .ToListAsync(ct);

        var positive = (await ledger.ActivatedBinsAsync(m => m.Lot.Item.Category == Cat.ElectrodeFiller, ct))
            .Where(b => b.Kg > 0)
            .ToList();
        var lotIds = positive.Select(b => b.LotId).Distinct().ToList();
        var lots = await db.ConsumableItemLots.AsNoTracking()
            .Where(l => lotIds.Contains(l.Id))
            .Select(l => new { l.Id, l.ItemId, l.Brand, l.LotNumber, l.Item.Category, l.Item.Diameter, l.Item.Specification, l.Item.HoldingOvenType })
            .ToDictionaryAsync(l => l.Id, ct);

        var since = (await ledger.Live()
                .Where(m => m.ToStage == Cat.Activated && m.ToCompartmentId != null && lotIds.Contains(m.LotId))
                .GroupBy(m => new { m.ToCompartmentId, m.LotId })
                .Select(g => new { g.Key.ToCompartmentId, g.Key.LotId, Last = g.Max(m => m.CreatedAt) })
                .ToListAsync(ct))
            .ToDictionary(x => (x.ToCompartmentId, x.LotId), x => x.Last);

        BinLotDto ToLot(BinRow b)
        {
            var l = lots[b.LotId];
            DateTime? at = since.TryGetValue((b.CompartmentId, b.LotId), out var last) ? last : null;
            return new BinLotDto(l.ItemId, Cat.DiaSpec(l.Diameter, l.Specification), l.Category, b.LotId, l.Brand, l.LotNumber,
                b.Kg, at, b.CompartmentId, l.HoldingOvenType, l.Specification, Cat.FormatDiameter(l.Diameter));
        }

        var byBin = positive.Where(b => b.CompartmentId is not null).ToLookup(b => b.CompartmentId!.Value);
        var ovenDtos = ovens
            .Select(o => new OvenDto(o.Id, o.Name, o.Code, o.OvenType,
                o.Compartments
                    .Select(c =>
                    {
                        var contents = byBin[c.Id].OrderBy(b => b.LotId).Select(ToLot).ToList();
                        return new CompartmentDto(c.Id, o.Id, c.Number, c.Label, $"{o.Code}-{c.Label}", contents.Sum(x => x.Kg),
                            contents.Where(x => x.SinceAt is not null).Min(x => x.SinceAt), contents);
                    })
                    .ToList()))
            .ToList();

        var unassigned = positive.Where(b => b.CompartmentId is null).OrderBy(b => b.LotId).Select(ToLot).ToList();
        return new OvenBoardDto(ovenDtos, unassigned);
    }
}
