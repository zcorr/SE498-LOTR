using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LotrApi.Tests;

/// <summary>
/// Integration tests for SPEC API routes against a real PostgreSQL instance (Testcontainers).
/// </summary>
public class ApiEndpointsIntegrationTests : IClassFixture<LotrApiPostgresFixture>
{
    private readonly HttpClient _client;

    public ApiEndpointsIntegrationTests(LotrApiPostgresFixture fixture) =>
        _client = fixture.Client;

    // --- Server health (SCRUM-11 / SPEC: GET /health = API liveness) ---

    [Fact]
    public async Task GetHealth_Returns200_ForServerHealth()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Game data routes per SPEC (server liveness is GET /health above) ---

    [Fact]
    public async Task GetClassById_Returns200_AndShapeFromSpec()
    {
        var response = await _client.GetAsync("/class/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("name", out _));
        Assert.True(root.TryGetProperty("desc", out _));
        Assert.True(root.TryGetProperty("racialids", out var ids) && ids.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task GetStats_Returns200_AndJsonArray()
    {
        var response = await _client.GetAsync("/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetStatByName_Returns200_AndSingleStatRecord()
    {
        var response = await _client.GetAsync("/stats/strength");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("strength", root.GetProperty("name").GetString());
        Assert.True(root.GetProperty("id").GetInt32() > 0);
        Assert.Equal(10, root.GetProperty("baseValue").GetInt32());
    }

    [Fact]
    public async Task GetStatByName_UnknownStat_Returns404()
    {
        var response = await _client.GetAsync("/stats/not-a-real-stat");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStatByName_IsCaseInsensitive()
    {
        var response = await _client.GetAsync("/stats/Strength");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("strength", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetCharHealth_Returns200_CharacterHealthPayload()
    {
        var response = await _client.GetAsync("/charhealth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task GetStrength_Returns200_AndNonEmptyPayload()
    {
        var response = await _client.GetAsync("/strength");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task GetAbilities_Returns200_AndJsonArray()
    {
        var response = await _client.GetAsync("/abilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetRace_Returns200_AndJsonArray()
    {
        var response = await _client.GetAsync("/race");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetPremades_Returns200_AndPaginatedPayload()
    {
        var response = await _client.GetAsync("/premades");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("items").ValueKind);
        Assert.Equal(3, root.GetProperty("total").GetInt32());
        Assert.Equal(20, root.GetProperty("limit").GetInt32());
        Assert.Equal(0, root.GetProperty("offset").GetInt32());
    }

    [Fact]
    public async Task GetNames_Returns200_AndJsonArrayOfStrings()
    {
        var response = await _client.GetAsync("/names");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetPremades_FiltersByClassId()
    {
        var response = await _client.GetAsync("/premades?class_id=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("total").GetInt32());

        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(3, item.GetProperty("class_id").GetInt32());
        }
    }

    [Fact]
    public async Task GetPremades_FiltersByRaceId()
    {
        var response = await _client.GetAsync("/premades?race_id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("total").GetInt32());

        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(1, item.GetProperty("race_id").GetInt32());
        }
    }

    [Fact]
    public async Task GetPremades_SearchesByNameSubstring_CaseInsensitive()
    {
        var response = await _client.GetAsync("/premades?q=GOL");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("total").GetInt32());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal("Gollum", item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetPremades_PaginationBeyondTotal_ReturnsEmptyItemsAndOriginalTotal()
    {
        var response = await _client.GetAsync("/premades?limit=5&offset=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(3, root.GetProperty("total").GetInt32());
        Assert.Equal(5, root.GetProperty("limit").GetInt32());
        Assert.Equal(5, root.GetProperty("offset").GetInt32());
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetPremades_InvalidLimit_Returns400(int limit)
    {
        var response = await _client.GetAsync($"/premades?limit={limit}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetNames_AppliesTheSameFilters()
    {
        var response = await _client.GetAsync("/names?race_id=1&q=go");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("Gollum", names.GetString());
    }

    [Fact]
    public async Task PostGenerate_ValidBody_Returns200_AndCharacterSheetObject()
    {
        var response = await _client.PostAsJsonAsync("/generate", new { class_id = 1, race_id = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task PostGenerate_InvalidBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/generate", new { class_id = -1, race_id = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// SPEC NFR: response under 1s — adjust or mark Explicit if CI is noisy.
    /// </summary>
    [Fact]
    public async Task GetClassById_CompletesWithinOneSecond()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _client.GetAsync("/class/1");
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Elapsed {sw.ElapsedMilliseconds}ms");
    }
}
