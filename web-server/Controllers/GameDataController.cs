using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_server.Services;

namespace web_server.Controllers;

[ApiController]
[Route("api/gamedata")]
public class GameDataController : ControllerBase
{
    private readonly ILotrApiClient _apiClient;

    public GameDataController(ILotrApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("class/{id}")]
    public async Task<IActionResult> GetClass(int id)
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var classData = await _apiClient.GetClassAsync(id, token);
        if (classData == null)
            return NotFound();

        return Ok(classData);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var stats = await _apiClient.GetStatsAsync(token);
        if (stats == null)
            return StatusCode(500, "Failed to fetch stats");

        return Ok(stats);
    }

    [HttpGet("charhealth")]
    public async Task<IActionResult> GetCharacterHealth()
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var health = await _apiClient.GetCharacterHealthAsync(token);
        if (health == null)
            return StatusCode(500, "Failed to fetch character health");

        return Ok(health);
    }

    [HttpGet("strength")]
    public async Task<IActionResult> GetStrength()
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var strength = await _apiClient.GetStrengthAsync(token);
        if (strength == null)
            return StatusCode(500, "Failed to fetch strength");

        return Ok(strength);
    }

    [HttpGet("race")]
    public async Task<IActionResult> GetRaces()
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var races = await _apiClient.GetRacesAsync(token);
        if (races == null)
            return StatusCode(500, "Failed to fetch races");

        return Ok(races);
    }
}