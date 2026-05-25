using Microsoft.AspNetCore.Mvc;
using Stoloto.LogParser.Core.Models;
using Stoloto.LogParser.Web.Services;

namespace Stoloto.LogParser.Web.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(SettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(settingsService.Load());

    [HttpPut]
    public IActionResult Put([FromBody] UserSettings settings)
    {
        settingsService.Save(settings);
        return Ok(settingsService.Load());
    }
}
