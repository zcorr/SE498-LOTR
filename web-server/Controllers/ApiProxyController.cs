using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_server.Services;

// API controller for direct API server proxy endpoints
[ApiController]
[Route("api/proxy")]
public class ApiProxyController : ControllerBase
{
    private readonly ILotrApiClient _apiClient;

    public ApiProxyController(ILotrApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var isHealthy = await _apiClient.IsHealthyAsync();
        if (isHealthy)
            return Ok(new { status = "ok" });
        else
            return StatusCode(500, "API server unhealthy");
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

    [HttpGet("abilities")]
    public async Task<IActionResult> GetAbilities([FromQuery(Name = "class_id")] int? classId)
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        if (classId < 0)
            return BadRequest("class_id must be a non-negative integer");

        var abilities = await _apiClient.GetAbilitiesAsync(token, classId);
        if (abilities == null)
            return StatusCode(500, "Failed to fetch abilities");

        return Ok(abilities);
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

    [HttpGet("premades")]
    public async Task<IActionResult> GetPremades()
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var premades = await _apiClient.GetPremadesAsync(token);
        if (premades == null)
            return StatusCode(500, "Failed to fetch premades");

        return Ok(premades);
    }

    [HttpGet("names")]
    public async Task<IActionResult> GetNames()
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var names = await _apiClient.GetNamesAsync(token);
        if (names == null)
            return StatusCode(500, "Failed to fetch names");

        return Ok(names);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateCharacter([FromBody] GenerateRequest request)
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        if (request.ClassId <= 0 || request.RaceId <= 0)
            return BadRequest("Valid class_id and race_id are required");

        var character = await _apiClient.GenerateCharacterAsync(request.ClassId, request.RaceId, token);
        if (character == null)
            return StatusCode(500, "Failed to generate character");

        return Ok(character);
    }

    public class GenerateRequest
    {
        public int ClassId { get; set; }
        public int RaceId { get; set; }
    }
}
