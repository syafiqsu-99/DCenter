using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/consumables")]
public class ConsumableOvensController(BakingService baking, OvenService ovens) : ConsumableControllerBase
{
    [HttpGet("baking/board")]
    public async Task<ActionResult<List<BakingRecordDto>>> Board(CancellationToken ct)
        => Ok(await baking.GetBoardAsync(ct));

    [HttpGet("baking")]
    [SupervisorOnly]
    public async Task<ActionResult<BakingPage>> BakingRecords([FromQuery] BakingQuery query, CancellationToken ct)
        => ToAction(await baking.GetRecordsAsync(query, ct));

    [HttpPost("baking")]
    public Task<ActionResult<BakingResult>> SendToBake(SendToBakeRequest request, CancellationToken ct)
        => Locked(() => baking.SendToBakeAsync(request, EnteredBy, ct));

    [HttpPost("baking/start")]
    public Task<ActionResult<BakingResult>> Start(BakingTimesRequest request, CancellationToken ct)
        => Locked(() => baking.StartAsync(request, EnteredBy, ct));

    [HttpPost("baking/stop")]
    public Task<ActionResult<BakingResult>> Stop(BakingTimesRequest request, CancellationToken ct)
        => Locked(() => baking.StopAsync(request, EnteredBy, ct));

    [HttpPut("baking/{id:int}")]
    [SupervisorOnly]
    public Task<ActionResult<BakingRecordDto>> UpdateBaking(int id, BakingUpdate request, CancellationToken ct)
        => Locked(() => baking.UpdateAsync(id, request, EnteredBy, ct));

    [HttpPost("holding")]
    public Task<ActionResult<PlaceResult>> Place(PlaceRequest request, CancellationToken ct)
        => Locked(() => baking.PlaceAsync(request, EnteredBy, ct));

    [HttpGet("holding")]
    [SupervisorOnly]
    public async Task<ActionResult<HoldingPage>> Holdings([FromQuery] HoldingQuery query, CancellationToken ct)
        => Ok(await baking.GetHoldingsAsync(query, ct));

    [HttpGet("ovens")]
    public async Task<ActionResult<OvenBoardDto>> Ovens(CancellationToken ct)
        => Ok(await ovens.GetBoardAsync(ct));
}
