using System.Text.Json;

namespace web_server.Services;

public interface ILotrApiClient
{
    Task<bool> IsHealthyAsync();
    Task<ClassDTO?> GetClassAsync(int id, string bearerToken);
    Task<List<StatDTO>> GetStatsAsync(string bearerToken);
    Task<List<RaceDTO>> GetRacesAsync(string bearerToken);
    Task<List<AbilityDTO>> GetAbilitiesAsync(string bearerToken, int? classId = null);
    Task<List<PremadeDTO>> GetPremadesAsync(string bearerToken);
    Task<GeneratedCharacterSheetDTO?> GenerateCharacterAsync(int classId, int raceId, string bearerToken);
    Task<string?> GetCharacterHealthAsync(string bearerToken);
    Task<string?> GetStrengthAsync(string bearerToken);
    Task<string?> GetNamesAsync(string bearerToken);
    Task<List<ClassDTO>> GetClassesAsync(string bearerToken);
}
