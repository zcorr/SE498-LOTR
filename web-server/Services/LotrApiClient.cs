using System.Text.Json;

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

    public async Task<List<PremadeDTO>> GetPremadesAsync(string bearerToken)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync("/premades");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<List<PremadeDTO>>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return result ?? new List<PremadeDTO>();
            }
        }
        catch { }
        return new List<PremadeDTO>();
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

    public async Task<string?> GetNamesAsync(string bearerToken)
    {
        AddAuthHeader(bearerToken);
        try
        {
            var response = await _httpClient.GetAsync("/names");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch { }
        return null;
    }
}