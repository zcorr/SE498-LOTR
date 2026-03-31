using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LotrApi.Tests;

/// <summary>
/// Integration tests for SPEC API routes (red phase: API has no mappings yet).
/// Implement endpoints in Program.cs (or controllers) until these pass.
/// </summary>
public class ApiEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointsIntegrationTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    // --- Process liveness (SCRUM-11 style; avoids clashing with game GET /health in SPEC) ---

    [Fact]
    public async Task GetLive_Returns200_ForProcessHealth()
    {
        var response = await _client.GetAsync("/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- SPEC table: 10 endpoints ---

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
    public async Task GetHealth_Returns200_CharacterHealthPayload()
    {
        var response = await _client.GetAsync("/health");

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
    public async Task GetSpecies_Returns200_AndJsonArray()
    {
        var response = await _client.GetAsync("/species");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetPremades_Returns200_AndJsonArray()
    {
        var response = await _client.GetAsync("/premades");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
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
