using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/consumables")]
public class ConsumablesController(
    ConsumableItemService items, ConsumableMovementService movements, ConsumableQueryService queries,
    ConsumableImportService imports) : ConsumableControllerBase
{
    [HttpGet("catalog")]
    public ActionResult<StockCatalogDto> Catalog() => Ok(queries.GetCatalog());

    [HttpGet("items")]
    public async Task<ActionResult<List<ItemDto>>> SearchItems(
        [FromQuery] string? q, [FromQuery] string? category, [FromQuery] bool activeOnly = true,
        [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (!ConsumableText.TryCategoryFilter(category, out var cat)) return BadRequest("Unknown consumable type.");
        return Ok(await items.SearchAsync(q, cat, activeOnly, take, ct));
    }

    [HttpPost("items")]
    [SupervisorOnly]
    public Task<ActionResult<ItemDto>> CreateItem(ItemUpsert dto, CancellationToken ct)
        => Locked(() => items.UpsertAsync(null, dto, ct));

    [HttpPut("items/{id:int}")]
    [SupervisorOnly]
    public Task<ActionResult<ItemDto>> UpdateItem(int id, ItemUpsert dto, CancellationToken ct)
        => Locked(() => items.UpsertAsync(id, dto, ct));

    [HttpGet("items/{id:int}/lots")]
    public async Task<ActionResult<List<LotOption>>> Lots(int id, CancellationToken ct)
        => Ok(await queries.GetLotsAsync(id, ct));

    [HttpGet("items/{id:int}/lot-balances")]
    public async Task<ActionResult<List<LotBalanceDto>>> LotBalances(int id, [FromQuery] bool includeZero = false, CancellationToken ct = default)
        => Ok(await queries.GetLotBalancesAsync(id, includeZero, ct));

    [HttpGet("items/{id:int}/recent-quantities")]
    public async Task<ActionResult<List<decimal>>> RecentQuantities(int id, [FromQuery] string? type, CancellationToken ct)
        => Ok(await queries.GetRecentQuantitiesAsync(id, type, ct));

    [HttpGet("balances")]
    [SupervisorOnly]
    public async Task<ActionResult<List<ItemBalanceDto>>> Balances(
        [FromQuery] string? category, [FromQuery] bool includeZero = false, CancellationToken ct = default)
        => ToAction(await queries.GetBalancesAsync(category, includeZero, ct));

    [HttpGet("lot-stock")]
    [SupervisorOnly]
    public async Task<ActionResult<List<LotStockRow>>> LotStock(
        [FromQuery] string? category, [FromQuery] bool includeZero = false, CancellationToken ct = default)
        => ToAction(await queries.GetLotStockAsync(category, includeZero, ct));

    [HttpGet("normal-stock")]
    public async Task<ActionResult<List<NormalStockDto>>> NormalStock([FromQuery] string? category, CancellationToken ct)
        => Ok(await queries.GetNormalStockAsync(category, ct));

    [HttpGet("counter")]
    public async Task<ActionResult<List<CounterItemDto>>> Counter(
        [FromQuery] int? welderId, [FromQuery] string? category, CancellationToken ct)
        => ToAction(await queries.GetCounterAsync(welderId, category, ct));

    [HttpGet("counter/welders/{welderId:int}/today")]
    public async Task<ActionResult<List<TransactionDto>>> WelderToday(int welderId, CancellationToken ct)
        => Ok(await queries.GetWelderTodayAsync(welderId, ct));

    [HttpPost("receive")]
    [SupervisorOnly]
    public Task<ActionResult<MovementResult>> Receive(ReceiveRequest request, CancellationToken ct)
        => Locked(() => movements.ReceiveAsync(request, EnteredBy, ct));

    [HttpPost("transfer")]
    [SupervisorOnly]
    public Task<ActionResult<MovementResult>> Transfer(TransferRequest request, CancellationToken ct)
        => Locked(() => movements.TransferAsync(request, EnteredBy, ct));

    [HttpPost("issue")]
    public Task<ActionResult<MovementResult>> Issue(IssueRequest request, CancellationToken ct)
        => Locked(() => movements.IssueAsync(request, EnteredBy, ct));

    [HttpPost("return")]
    public Task<ActionResult<MovementResult>> Return(ReturnRequest request, CancellationToken ct)
        => Locked(() => movements.ReturnAsync(request, EnteredBy, ct));

    [HttpPost("move")]
    [SupervisorOnly]
    public Task<ActionResult<MovementResult>> Move(MoveRequest request, CancellationToken ct)
        => Locked(() => movements.MoveAsync(request, EnteredBy, ct));

    [HttpPost("finish")]
    public Task<ActionResult<MovementResult>> Finish(FinishRequest request, CancellationToken ct)
        => Locked(() => movements.FinishAsync(request, EnteredBy, ct));

    [HttpPost("adjust")]
    [SupervisorOnly]
    public Task<ActionResult<MovementResult>> Adjust(AdjustRequest request, CancellationToken ct)
        => Locked(() => movements.AdjustAsync(request, EnteredBy, ct));

    [HttpPost("transactions/{txnNo}/void")]
    [SupervisorOnly]
    public Task<ActionResult<MovementResult>> Void(string txnNo, VoidRequest request, CancellationToken ct)
        => Locked(() => movements.VoidAsync(txnNo, request, EnteredBy, ct));

    [HttpGet("transactions")]
    [SupervisorOnly]
    public async Task<ActionResult<TransactionPage>> Transactions([FromQuery] TransactionQuery query, CancellationToken ct)
        => ToAction(await queries.GetTransactionsAsync(query, ct));

    [HttpGet("transactions/today")]
    [SupervisorOnly]
    public async Task<ActionResult<List<TransactionDto>>> EnteredToday([FromQuery] string? type, CancellationToken ct)
        => ToAction(await queries.GetEnteredTodayAsync(type, ct));

    [HttpGet("dashboard")]
    [SupervisorOnly]
    public async Task<ActionResult<DashboardDto>> Dashboard([FromQuery] string? category, CancellationToken ct)
        => ToAction(await queries.GetDashboardAsync(category, ct));

    [HttpGet("items/export")]
    [SupervisorOnly]
    public async Task<IActionResult> ExportItems([FromQuery] bool template = false, CancellationToken ct = default)
    {
        var bytes = await imports.ExportAsync(template, ct);
        var name = template ? "Consumables template.csv" : $"Consumables {DateTime.Now:yyyy-MM-dd}.csv";
        return File(bytes, "text/csv; charset=utf-8", name);
    }

    [HttpPost("items/import")]
    [SupervisorOnly]
    [RequestSizeLimit(ConsumableImportService.MaxFileBytes + 64 * 1024)]
    public async Task<ActionResult<ImportResultDto>> ImportItems(
        IFormFile? file, [FromQuery] bool commit = false, [FromQuery] bool skipInvalid = false, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest("Choose a CSV file to import.");
        if (file.Length > ConsumableImportService.MaxFileBytes) return BadRequest("The file is larger than 2 MB.");
        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return BadRequest("Only .csv files can be imported.");

        await using var stream = file.OpenReadStream();
        return await Locked(() => imports.ImportAsync(stream, commit, skipInvalid, ct));
    }
}
