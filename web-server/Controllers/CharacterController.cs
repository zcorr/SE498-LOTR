using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_server.Services;
using System.IO;

namespace web_server.Controllers;

[Authorize] // Require authentication for all character operations
public class CharacterController : Controller
{
    private readonly ILotrApiClient _apiClient;

    public CharacterController(ILotrApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // GET: /Character/Create - Serve the character creation page
    public IActionResult Create()
    {
        return PhysicalFile(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "create.html"),
            "text/html"
        );
    }

    // GET: /Character/Sheet - Serve the character sheet display page
    public IActionResult Sheet()
    {
        return PhysicalFile(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "character-sheet.html"),
            "text/html"
        );
    }
}

// API controller for character operations
[ApiController]
[Route("api/character")]
public class CharacterApiController : ControllerBase
{
    private readonly ILotrApiClient _apiClient;

    public CharacterApiController(ILotrApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("races")]
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

    [HttpGet("classes")]
    public async Task<IActionResult> GetClasses()
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var classes = await _apiClient.GetAbilitiesAsync(token); // This should be classes, but using abilities for now
        if (classes == null)
            return StatusCode(500, "Failed to fetch classes");

        return Ok(classes);
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
