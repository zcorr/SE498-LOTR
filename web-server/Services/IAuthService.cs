namespace web_server.Services;

public interface IAuthService
{
    Task<string?> AuthenticateUserAsync(string username, string password);
    bool ValidateToken(string token);
    string? GetUserFromToken(string token);
}