using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace web_server.Services;

public class LotrApiClient : ILotrApiClient
{
    private readonly HttpClient _httpClient;

    public LotrApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void AddAuthHeader(string bearerToken)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }

    private static string BuildPremadeQueryString(
        string path,
        int? classId = null,
        int? raceId = null,
        string? query = null,
        int? limit = null,
        int? offset = null)
    {
        var parameters = new Dictionary<string, string?>();

        if (classId.HasValue)
            parameters["class_id"] = classId.Value.ToString();
        if (raceId.HasValue)
            parameters["race_id"] = raceId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(query))
            parameters["q"] = query.Trim();
        if (limit.HasValue)
            parameters["limit"] = limit.Value.ToString();
        if (offset.HasValue)
            parameters["offset"] = offset.Value.ToString();

        return parameters.Count == 0
            ? path
            : QueryHelpers.AddQueryString(path, parameters!);
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ClassDTO?> GetClassAsync(int id, string bearerToken)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync($"/class/{id}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ClassDTO>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
        }
        catch { }
        return null;
    }

    public async Task<List<StatDTO>> GetStatsAsync(string bearerToken)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync("/stats");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<List<StatDTO>>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return result ?? new List<StatDTO>();
            }
        }
        catch { }
        return new List<StatDTO>();
    }

    public async Task<List<RaceDTO>> GetRacesAsync(string bearerToken)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync("/race");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<List<RaceDTO>>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return result ?? new List<RaceDTO>();
            }
        }
        catch { }
        return new List<RaceDTO>();
    }

    public async Task<List<AbilityDTO>> GetAbilitiesAsync(string bearerToken)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync("/abilities");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<List<AbilityDTO>>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return result ?? new List<AbilityDTO>();
            }
        }
        catch { }
        return new List<AbilityDTO>();
    }

    public async Task<PremadeListResponseDTO?> GetPremadesAsync(
        string bearerToken,
        int? classId = null,
        int? raceId = null,
        string? query = null,
        int? limit = null,
        int? offset = null)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync(
                BuildPremadeQueryString("/premades", classId, raceId, query, limit, offset));
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PremadeListResponseDTO>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
        }
        catch { }
        return null;
    }

    public async Task<GeneratedCharacterSheetDTO?> GenerateCharacterAsync(int classId, int raceId, string bearerToken)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var payload = new { class_id = classId, race_id = raceId };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/generate", content);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GeneratedCharacterSheetDTO>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
        }
        catch { }
        return null;
    }

    public async Task<string?> GetCharacterHealthAsync(string bearerToken)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync("/charhealth");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch { }
        return null;
    }

    public async Task<string?> GetStrengthAsync(string bearerToken)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync("/strength");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch { }
        return null;
    }

    public async Task<List<string>?> GetNamesAsync(
        string bearerToken,
        int? classId = null,
        int? raceId = null,
        string? query = null)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync(
                BuildPremadeQueryString("/names", classId, raceId, query));
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<string>>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
        }
        catch { }
        return null;
    }
}
