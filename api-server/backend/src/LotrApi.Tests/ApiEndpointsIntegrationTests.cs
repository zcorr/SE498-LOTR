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
    private readonly LotrApiPostgresFixture _fixture;

    public ApiEndpointsIntegrationTests(LotrApiPostgresFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

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
    public async Task GetStatByName_Returns200_ForKnownStat()
    {
        var response = await _client.GetAsync("/stats/strength");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("strength", root.GetProperty("name").GetString());
        Assert.True(root.GetProperty("id").GetInt32() > 0);
        Assert.True(root.GetProperty("baseValue").GetInt32() > 0);
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
    public async Task GetStatByName_Returns404_ForUnknownStat()
    {
        var response = await _client.GetAsync("/stats/not-a-real-stat");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
    public async Task GetPremades_Returns200_AndJsonArray()
    {
        var response = await _client.GetAsync("/premades");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetPremades_ReturnsSeededLotrLibrary_WithCompleteStatsAndCoverage()
    {
        var response = await _client.GetAsync("/premades");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var premades = doc.RootElement.EnumerateArray().ToList();
        var expectedNames = new[]
        {
            "Gandalf the Grey",
            "Frodo",
            "Samwise Gamgee",
            "Aragorn",
            "Legolas",
            "Gimli",
            "Boromir",
            "Gollum",
            "Galadriel",
            "Saruman the White",
        };
        var expectedStats = new[]
        {
            "charhealth",
            "strength",
            "dexterity",
            "constitution",
            "intelligence",
            "wisdom",
            "charisma",
        };

        Assert.True(premades.Count >= 10, $"Expected at least 10 premades but found {premades.Count}.");
        var names = premades
            .Select(p => p.GetProperty("name").GetString())
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedName in expectedNames)
            Assert.Contains(expectedName, names);

        var distinctClassIds = new HashSet<int>();
        var distinctRaceIds = new HashSet<int>();

        foreach (var premade in premades)
        {
            var classId = premade.GetProperty("class_id").GetInt32();
            var raceId = premade.GetProperty("race_id").GetInt32();
            var stats = premade.GetProperty("stats");

            Assert.True(classId > 0);
            Assert.True(raceId > 0);
            Assert.Equal(JsonValueKind.Object, stats.ValueKind);

            foreach (var statName in expectedStats)
                Assert.True(stats.TryGetProperty(statName, out _), $"Missing stat '{statName}' for {premade.GetProperty("name").GetString()}.");

            distinctClassIds.Add(classId);
            distinctRaceIds.Add(raceId);
        }

        Assert.True(distinctClassIds.Count >= 3, $"Expected at least 3 classes but found {distinctClassIds.Count}.");
        Assert.True(distinctRaceIds.Count >= 3, $"Expected at least 3 races but found {distinctRaceIds.Count}.");
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
    public async Task GetNames_ReturnsEverySeededLotrName()
    {
        var response = await _client.GetAsync("/names");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = doc.RootElement
            .EnumerateArray()
            .Select(name => name.GetString())
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedName in new[]
                 {
                     "Gandalf the Grey",
                     "Frodo",
                     "Samwise Gamgee",
                     "Aragorn",
                     "Legolas",
                     "Gimli",
                     "Boromir",
                     "Gollum",
                     "Galadriel",
                     "Saruman the White",
                 })
        {
            Assert.Contains(expectedName, names);
        }
    }

    [Fact]
    public async Task DatabaseBootstrap_CanRunTwice_WithoutDuplicatingPremades()
    {
        var before = await GetPremadeNamesAsync();

        await DatabaseBootstrap.ApplyAsync(_fixture.ConnectionString);

        var after = await GetPremadeNamesAsync();

        Assert.Equal(before.Count, after.Count);
        Assert.Equal(after.Count, after.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(before.OrderBy(name => name), after.OrderBy(name => name));
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

    private async Task<List<string>> GetPremadeNamesAsync()
    {
        using var doc = JsonDocument.Parse(await (await _client.GetAsync("/premades")).Content.ReadAsStringAsync());
        return doc.RootElement
            .EnumerateArray()
            .Select(premade => premade.GetProperty("name").GetString())
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}
