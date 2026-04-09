using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_server.Services;
using System.IO;

namespace web_server.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // GET: /Auth/Login - Serve the frontend login page
    [AllowAnonymous]
    public IActionResult Login()
    {
        // Check if user is already authenticated via cookie
        var token = Request.Cookies["AuthToken"];
        if (!string.IsNullOrWhiteSpace(token) && _authService.ValidateToken(token))
        {
            return Redirect("/Premade");
        }

        try
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "login.html");
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Login page not found");
            }
            return PhysicalFile(filePath, "text/html");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error serving login page: {ex.Message}");
        }
    }
}

// Separate API controller for authentication endpoints
[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthApiController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required");

        var token = await _authService.AuthenticateUserAsync(request.Username, request.Password);
        if (token == null)
            return Unauthorized("Invalid credentials");

        // Store token in cookie
        Response.Cookies.Append(
            "AuthToken",
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            });

        return Ok(new { message = "Login successful", token });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("AuthToken");
        return Ok(new { message = "Logged out" });
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}