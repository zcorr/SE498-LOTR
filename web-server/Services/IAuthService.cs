namespace web_server.Services;

public interface IAuthService
{
	Task<string?> AuthenticateUserAsync(string username, string password);
	Task<bool> RegisterUserAsync(string username, string password);
	Task SeedDefaultUserAsync();
	bool ValidateToken(string token);
	string? GetUserFromToken(string token);
}