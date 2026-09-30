using DCenter.Server.Models;
using Cat = DCenter.Server.Entities.StockCatalog;

namespace DCenter.Server.Services;

public class OvenService(ConsumableStore store, ConsumableLedger ledger)
{
    public async Task<OvenBoardDto> GetBoardAsync(CancellationToken ct)
    {
        var ovens = (await store.OvenCompartmentsAsync(null, ct))
            .GroupBy(r => r.OvenId)
            .Select(g => new
            {
                Id = g.Key, g.First().Name, g.First().Code, g.First().OvenType,
                Compartments = g.Where(r => r.CompartmentId is not null)
                    .Select(r => new { Id = r.CompartmentId!.Value, Number = r.Number!.Value, Label = r.Label! }).ToList(),
            })
            .ToList();

        var positive = (await ledger.ActivatedBinsAsync(LedgerFilter.ForCategory(Cat.ElectrodeFiller), ct))
            .Where(b => b.Kg > 0)
            .ToList();
        var lotIds = positive.Select(b => b.LotId).Distinct().ToList();
        var lots = (await store.LotsAsync(lotIds, null, null, ct)).ToDictionary(l => l.Id);

        var since = positive.Where(b => b.CompartmentId is not null && b.LastInAt is not null)
            .ToDictionary(b => (b.CompartmentId, b.LotId), b => b.LastInAt!.Value);

        BinLotDto ToLot(BinRow b)
        {
            var l = lots[b.LotId];
            DateTime? at = since.TryGetValue((b.CompartmentId, b.LotId), out var last) ? last : null;
            return new BinLotDto(l.ItemId, Cat.DiaSpec(l.Diameter, l.Specification), l.Category, b.LotId, l.Brand, l.LotNumber,
                b.Kg, at, b.CompartmentId, l.HoldingOvenType, l.Specification, l.Diameter);
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
