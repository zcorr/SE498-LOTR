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
    public async Task GetAbilities_WithClassIdQuery_ReturnsOnlyMatchingAbilities()
    {
        var response = await _client.GetAsync("/abilities?class_id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());
        Assert.All(root.EnumerateArray(), ability =>
        {
            Assert.Equal(1, ability.GetProperty("class_id").GetInt32());
        });
    }

    [Fact]
    public async Task GetAbilities_WithUnknownClassId_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/abilities?class_id=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task GetAbilities_WithoutClassId_ReturnsFullList_ForBackwardCompatibility()
    {
        var response = await _client.GetAsync("/abilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(8, doc.RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData("/abilities?class_id=-1")]
    [InlineData("/abilities?class_id=not-a-number")]
    public async Task GetAbilities_WithInvalidClassId_Returns400(string route)
    {
        var response = await _client.GetAsync(route);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetClassAbilitiesRoute_ReturnsSameScopedList()
    {
        var response = await _client.GetAsync("/class/2/abilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(2, root.GetArrayLength());
        Assert.All(root.EnumerateArray(), ability =>
        {
            Assert.Equal(2, ability.GetProperty("class_id").GetInt32());
        });
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
