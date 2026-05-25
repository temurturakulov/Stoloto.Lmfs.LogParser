using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Stoloto.LogParser.Core.Models;
using Stoloto.LogParser.Web.Services;

namespace Stoloto.LogParser.Web.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController(LogQueryService queryService, LiveLogService liveService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string path,
        [FromQuery] bool isFile = false,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] string? levels = null,
        [FromQuery] string? categories = null,
        [FromQuery] string? type = null,
        [FromQuery] string? uid = null,
        [FromQuery] string? urlContains = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] bool sortAsc = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path) ||
            (!isFile && !Directory.Exists(path)) ||
            (isFile && !System.IO.File.Exists(path)))
            return BadRequest(new { error = $"Путь не найден: {path}" });

        var query = new LogQuery
        {
            Path = path,
            IsFile = isFile,
            DateFrom = dateFrom != null ? DateTime.Parse(dateFrom) : null,
            DateTo = dateTo != null ? DateTime.Parse(dateTo) : null,
            Levels = levels?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Categories = categories?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Type = type,
            Uid = uid,
            UrlContains = urlContains,
            Search = search,
            Page = page,
            PageSize = pageSize,
            SortAsc = sortAsc
        };

        var result = await queryService.QueryAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(
        [FromQuery] string path,
        [FromQuery] bool isFile = false,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path) ||
            (!isFile && !Directory.Exists(path)) ||
            (isFile && !System.IO.File.Exists(path)))
            return BadRequest(new { error = $"Путь не найден: {path}" });

        DateTime? from = dateFrom != null ? DateTime.Parse(dateFrom) : null;
        DateTime? to   = dateTo   != null ? DateTime.Parse(dateTo)   : null;
        var result = await queryService.StatsAsync(path, isFile, from, to, ct);
        return Ok(result);
    }

    [HttpGet("trace/{uid}")]
    public async Task<IActionResult> Trace(
        string uid,
        [FromQuery] string path,
        [FromQuery] bool isFile = false,
        [FromQuery] string? date = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
            return BadRequest(new { error = "Укажите path" });

        DateTime? dateValue = date != null ? DateTime.Parse(date) : null;
        var result = await queryService.TraceAsync(uid, path, isFile, dateValue, ct);
        return Ok(result);
    }

    [HttpGet("live")]
    public async Task Live(
        [FromQuery] string path,
        [FromQuery] bool isFile = false,
        [FromQuery] string? levels = null,
        [FromQuery] string? categories = null,
        CancellationToken ct = default)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var levelList = levels?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];
        var categoryList = categories?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];

        var sessionId = liveService.AddSession(path, isFile, levelList, categoryList);
        var session = liveService.GetSession(sessionId)!;

        try
        {
            await foreach (var entries in session.Channel.Reader.ReadAllAsync(ct))
            {
                var json = JsonSerializer.Serialize(new { entries }, JsonOpts);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        finally
        {
            liveService.RemoveSession(sessionId);
        }
    }
}
