using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_server.Services;

namespace web_server.Controllers;

[Authorize]
[Route("my-characters")]
public class MyCharactersController : Controller
{
    // GET: /my-characters — Serve the my characters page
    [HttpGet("")]
    public IActionResult Index()
    {
        return PhysicalFile(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "my-characters.html"),
            "text/html"
        );
    }
}

[ApiController]
[Route("api/character/sheets")]
public class CharacterSheetApiController : ControllerBase
{
    private readonly ICharacterSheetService _sheetService;
    private readonly IAuthService _authService;

    public CharacterSheetApiController(ICharacterSheetService sheetService, IAuthService authService)
    {
        _sheetService = sheetService;
        _authService = authService;
    }

    // Extract user ID from the JWT stored in the auth cookie
    private int? GetUserIdFromCookie()
    {
        var token = Request.Cookies["AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            // JWT stores NameIdentifier as "sub", not the full ClaimTypes URI
            var idClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == "sub" || c.Type == "nameid" || c.Type == ClaimTypes.NameIdentifier);
            if (idClaim != null && int.TryParse(idClaim.Value, out var userId))
                return userId;
        }
        catch { }

        return null;
    }
    // POST /api/character/sheets — save a new character sheet
    [HttpPost("")]
    public async Task<IActionResult> SaveSheet([FromBody] SaveSheetRequest request)
    {
        var userId = GetUserIdFromCookie();
        if (userId == null)
            return Unauthorized("No valid authentication token found");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Character name is required");

        if (string.IsNullOrWhiteSpace(request.ClassName))
            return BadRequest("Class name is required");

        var sheetId = await _sheetService.SaveSheetAsync(userId.Value, request);
        return Ok(new { id = sheetId, message = "Character saved" });
    }

    // GET /api/character/sheets — get all sheets for the current user
    [HttpGet("")]
    public async Task<IActionResult> GetMySheets()
    {
        var userId = GetUserIdFromCookie();
        if (userId == null)
            return Unauthorized("No valid authentication token found");

        var sheets = await _sheetService.GetSheetsForUserAsync(userId.Value);
        return Ok(sheets);
    }

    // GET /api/character/sheets/{id} — get a specific sheet
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSheet(int id)
    {
        var userId = GetUserIdFromCookie();
        if (userId == null)
            return Unauthorized("No valid authentication token found");

        var sheet = await _sheetService.GetSheetByIdAsync(id, userId.Value);
        if (sheet == null)
            return NotFound();

        return Ok(sheet);
    }

    // DELETE /api/character/sheets/{id} — delete a sheet
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteSheet(int id)
    {
        var userId = GetUserIdFromCookie();
        if (userId == null)
            return Unauthorized("No valid authentication token found");

        var deleted = await _sheetService.DeleteSheetAsync(id, userId.Value);
        if (!deleted)
            return NotFound();

        return Ok(new { message = "Character deleted" });
    }
}