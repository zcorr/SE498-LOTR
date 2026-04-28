using System.Text.Json;

namespace web_server.Services;

public interface ILotrApiClient
{
    Task<bool> IsHealthyAsync();
    Task<ClassDTO?> GetClassAsync(int id, string bearerToken);
    Task<List<StatDTO>> GetStatsAsync(string bearerToken);
    Task<List<RaceDTO>> GetRacesAsync(string bearerToken);
    Task<List<AbilityDTO>> GetAbilitiesAsync(string bearerToken);
    Task<PremadeListResponseDTO?> GetPremadesAsync(
        string bearerToken,
        int? classId = null,
        int? raceId = null,
        string? query = null,
        int? limit = null,
        int? offset = null);
    Task<GeneratedCharacterSheetDTO?> GenerateCharacterAsync(int classId, int raceId, string bearerToken);
    Task<string?> GetCharacterHealthAsync(string bearerToken);
    Task<string?> GetStrengthAsync(string bearerToken);
    Task<List<string>?> GetNamesAsync(
        string bearerToken,
        int? classId = null,
        int? raceId = null,
        string? query = null);
}
