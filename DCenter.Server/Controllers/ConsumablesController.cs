using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/consumables")]
public class ConsumablesController(ConsumableInventoryService inventory) : ControllerBase
{
    private const long MaxImportBytes = 5 * 1024 * 1024;

    [HttpGet("catalog")]
    public ActionResult<ConsumableCatalogDto> Catalog() => Ok(inventory.GetCatalog());

    [HttpGet]
    public async Task<ActionResult<List<ConsumableDto>>> Search(
        [FromQuery] string? q, [FromQuery] bool activeOnly = true, [FromQuery] int take = 30, CancellationToken ct = default)
        => Ok(await inventory.SearchConsumablesAsync(q, activeOnly, take, ct));

    [HttpPost]
    public async Task<ActionResult<ConsumableDto>> Create(ConsumableUpsert dto, CancellationToken ct)
        => ToAction(await inventory.UpsertConsumableAsync(null, dto, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ConsumableDto>> Update(int id, ConsumableUpsert dto, CancellationToken ct)
        => ToAction(await inventory.UpsertConsumableAsync(id, dto, ct));

    [HttpGet("{id:int}/lots")]
    public async Task<ActionResult<List<LotOption>>> Lots(int id, CancellationToken ct)
        => Ok(await inventory.GetLotsAsync(id, ct));

    [HttpGet("{id:int}/recent-quantities")]
    public async Task<ActionResult<List<decimal>>> RecentQuantities(int id, CancellationToken ct)
        => Ok(await inventory.GetRecentQuantitiesAsync(id, ct));

    [HttpGet("stock")]
    public async Task<ActionResult<List<StockLotOption>>> Stock([FromQuery] string? location, CancellationToken ct)
        => ToAction(await inventory.GetStockAsync(location, ct));

    [HttpGet("stock-card")]
    public async Task<ActionResult<List<StockCardRow>>> StockCard(
        [FromQuery] string? location, [FromQuery] bool includeZero = false, CancellationToken ct = default)
        => ToAction(await inventory.GetStockCardAsync(location, includeZero, ct));

    [HttpPost("receive")]
    public Task<ActionResult<MovementResult>> Receive(ReceiveRequest request, CancellationToken ct)
        => Locked(() => inventory.ReceiveAsync(request, ct));

    [HttpPost("issue")]
    public Task<ActionResult<MovementResult>> Issue(IssueRequest request, CancellationToken ct)
        => Locked(() => inventory.IssueAsync(request, ct));

    [HttpPost("transactions/{id:int}/void")]
    public Task<ActionResult<MovementResult>> Void(int id, VoidRequest request, CancellationToken ct)
        => Locked(() => inventory.VoidAsync(id, request, ct));

    [HttpGet("transactions")]
    public async Task<ActionResult<TransactionPage>> Transactions([FromQuery] TransactionQuery query, CancellationToken ct)
        => ToAction(await inventory.GetTransactionsAsync(query, ct));

    [HttpGet("transactions/today")]
    public async Task<ActionResult<List<TransactionDto>>> EnteredToday([FromQuery] string? type, CancellationToken ct)
        => ToAction(await inventory.GetEnteredTodayAsync(type, ct));

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard([FromQuery] string? location, CancellationToken ct)
        => ToAction(await inventory.GetDashboardAsync(location, ct));

    [HttpPost("import")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<ImportResult>> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("Choose a CSV file to import.");
        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .csv files can be imported. In Excel use Save As → CSV UTF-8.");

        await using var stream = file.OpenReadStream();
        return await Locked(() => inventory.ImportOpeningAsync(stream, ct));
    }

    private async Task<ActionResult<T>> Locked<T>(Func<Task<ServiceResult<T>>> action)
    {
        try
        {
            return ToAction(await action());
        }
        catch (TimeoutException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private ActionResult<T> ToAction<T>(ServiceResult<T> result)
        => result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, result.Error);
}
