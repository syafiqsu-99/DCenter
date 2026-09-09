using DCenter.Server.Data;
using DCenter.Server.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class JobSearchService
{
    private readonly SourceContext _db;

    public JobSearchService(SourceContext db) => _db = db;

    // Filters the source query by job number. One job number can return many rows.
    public async Task<List<JobRow>> SearchAsync(string jobNumber, CancellationToken ct)
    {
        var query = from wod in _db.WorkOrderDetails
                    where wod.WoNumber == jobNumber
                    join bom in _db.BillOfMaterialOthers
                        on wod.AssemblyItem equals bom.Item into bomGroup
                    from bom in bomGroup.DefaultIfEmpty()
                    orderby wod.WoNumber
                    select new JobRow
                    {
                        JobNumber = wod.WoNumber,
                        AssemblyItem = wod.AssemblyItem,
                        ItemDesc = wod.ItemDesc,
                        Qty = wod.StartQuantity,
                        ChildPart = bom != null ? bom.Component : null,
                        ComponentDesc = bom != null ? bom.ComponentDesc : null
                    };

        return await query.AsNoTracking().ToListAsync(ct);
    }

    // Distinct part descriptions/numbers for autocomplete, seeded from the job's rows.
    public async Task<List<JobRow>> PartsForJobAsync(string jobNumber, CancellationToken ct)
        => await SearchAsync(jobNumber, ct);
}
