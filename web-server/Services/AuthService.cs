using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace web_server.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _config;
    private readonly NpgsqlDataSource _db;

    public AuthService(IConfiguration config, NpgsqlDataSource db)
    {
        _config = config;
        _db = db;
    }

    // ── Called once at startup to ensure admin user exists ──
    public async Task SeedDefaultUserAsync()
    {
        await using var conn = await _db.OpenConnectionAsync();

        // Check if admin already exists
        await using var checkCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM users WHERE username = $1",
            conn);
        checkCmd.Parameters.AddWithValue("admin");
        var count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0);

        if (count > 0)
            return;

        // Hash the default password and insert
        var hash = BCrypt.Net.BCrypt.HashPassword("password");
        await using var insertCmd = new NpgsqlCommand(
            "INSERT INTO users (username, password_hash) VALUES ($1, $2)",
            conn);
        insertCmd.Parameters.AddWithValue("admin");
        insertCmd.Parameters.AddWithValue(hash);
        await insertCmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> AuthenticateUserAsync(string username, string password)
    {
        // Look up the user in the database
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, password_hash FROM users WHERE username = $1",
            conn);
        cmd.Parameters.AddWithValue(username);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var userId = reader.GetInt32(0);
        var storedHash = reader.GetString(1);

        // Verify the password against the stored BCrypt hash
        if (!BCrypt.Net.BCrypt.Verify(password, storedHash))
            return null;

        // Password is correct — generate a JWT
        var jwtSecret = _config["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException("Jwt:Secret is not configured.");
        }
        var key = Encoding.ASCII.GetBytes(jwtSecret);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task<bool> RegisterUserAsync(string username, string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO users (username, password_hash) VALUES ($1, $2)",
            conn);
        cmd.Parameters.AddWithValue(username);
        cmd.Parameters.AddWithValue(hash);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            // Unique constraint violation — username already taken
            return false;
        }
    }

    public bool ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var jwtSecret = _config["Jwt:Secret"];
            if (string.IsNullOrWhiteSpace(jwtSecret))
            {
                throw new InvalidOperationException("Jwt:Secret is not configured.");
            }
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            }, out SecurityToken validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? GetUserFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return jwtToken?.Claims?
                .FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
        }
        catch
        {
            return null;
        }
    }
}