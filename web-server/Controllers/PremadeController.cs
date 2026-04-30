using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_server.Services;
using System.IO;

namespace web_server.Controllers;

[Authorize] // Require authentication for all premade operations
[Route("premade")]
public class PremadeController : Controller
{
    private readonly ILotrApiClient _apiClient;

    public PremadeController(ILotrApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // GET: /premade - Serve the premades page
    [HttpGet("")]
    public IActionResult Index()
    {
        // Serve the frontend premades.html file
        return PhysicalFile(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "premades.html"),
            "text/html"
        );
    }

    // GET: /Premade/Browse - Alias for Index
    public IActionResult Browse()
    {
        return Index();
    }
}

// API controller for premade character operations
[ApiController]
[Route("api/premade")]
public class PremadeApiController : ControllerBase
{
    private readonly ILotrApiClient _apiClient;

    public PremadeApiController(ILotrApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetPremades()
    {
        // Get the bearer token from the cookie
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        var premades = await _apiClient.GetPremadesAsync(token);
        if (premades == null)
            return StatusCode(500, "Failed to fetch premade characters");

        return Ok(premades);
    }

    [HttpPost("select/{id}")]
    public async Task<IActionResult> SelectPremade(int id)
    {
        // Get the bearer token from the cookie
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("No authentication token found");

        // For now, just return success - in a full implementation,
        // this would save the selected premade to the user's account
        return Ok(new { message = $"Selected premade character {id}" });
    }
}