// =============================================================================
// LOTR API — implementation checklist (you fill in; tests are written first/TDD)
// =============================================================================
//
// SCRUM-11 (skeleton): GET /health -> 200 for API server (process) health per SPEC.
//
// SCRUM-13 / DB: Register Npgsql + EF or raw ADO; connection string from
// Configuration (appsettings / env). Your SQL lives under src/database/.
//
// SCRUM-14: Run idempotent seed against API DB (Ranger/Mage premades, etc.).
//
// SCRUM-19: Validate Authorization: Bearer <token> on API; 401 if missing/invalid.
// Coordinate signing secret / JWT shape with Web Server (SCRUM-17).
//
// Endpoints (SPEC — map these when you implement):
//   GET  /class/{id}   -> name, desc, racialids (see SPEC example)
//   GET  /stats        -> character stat values from Stats (or related) table
//   GET  /health       -> API server health (liveness)
//   GET  /charhealth   -> character health values (SCRUM-23)
//   GET  /strength     -> strength values
//   GET  /abilities    -> abilities list (id, name, desc, class_id)
//   GET  /race         -> races (id, name, modifiers)
//   GET  /premades     -> premade sheets incl. class_id, race_id, stats JSON
//   GET  /names        -> available character name strings
//   POST /generate     -> body: class_id, race_id; returns full generated sheet
//
// NFR: keep handlers under ~1s (SPEC).
//
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using LotrApi;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.OpenApi;
using Npgsql;
using NpgsqlTypes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "LOTR API",
        Version = "v1",
        Description = "Lord of the Rings board game — game data and character generation (SPEC endpoints).",
    });
});

builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
    return NpgsqlDataSource.Create(cs);
});

builder.Services.AddOpenApi();

var app = builder.Build();

var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
    await DatabaseBootstrap.ApplyAsync(connectionString);

if (app.Environment.IsDevelopment())
{
    // OpenAPI 3 JSON (Microsoft.AspNetCore.OpenApi) — e.g. /openapi/v1.json
    app.MapOpenApi();
    // Swagger UI + Swashbuckle-generated document at /swagger/v1/swagger.json
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LOTR API v1");
    });
}

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

static string? NormalizeSearch(string? query) =>
    string.IsNullOrWhiteSpace(query) ? null : query.Trim();

static bool TryResolvePremadePaging(int? requestedLimit, int? requestedOffset, out int limit, out int offset)
{
    limit = requestedLimit ?? 20;
    offset = requestedOffset ?? 0;

    return limit is >= 1 and <= 100
        && offset >= 0;
}

static void AddPremadeFilterParameters(NpgsqlCommand command, int? classId, int? raceId, string? query)
{
    command.Parameters.Add("@class_id", NpgsqlDbType.Integer).Value =
        classId.HasValue ? classId.Value : DBNull.Value;
    command.Parameters.Add("@race_id", NpgsqlDbType.Integer).Value =
        raceId.HasValue ? raceId.Value : DBNull.Value;
    command.Parameters.Add("@q", NpgsqlDbType.Text).Value =
        query is null ? DBNull.Value : query;
}

static async Task<StatRecord?> GetStatByNameAsync(string name, NpgsqlDataSource ds, CancellationToken cancellationToken = default)
{
    await using var conn = await ds.OpenConnectionAsync(cancellationToken);
    await using var cmd = new NpgsqlCommand(
        """
        SELECT id, name, base_value
        FROM stats
        WHERE lower(name) = lower($1)
        """,
        conn);
    cmd.Parameters.AddWithValue(name);
    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
        return null;

    return new StatRecord(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetInt32(2));
}

// Server health (SCRUM-11 / SPEC).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Health")
    .WithSummary("API process liveness (not character health — use GET /charhealth).");

app.MapGet("/class/{id:int}", async (int id, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        """
        SELECT name, description, racial_ids
        FROM classes
        WHERE id = $1
        """,
        conn);
    cmd.Parameters.AddWithValue(id);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.NotFound();

    var name = reader.GetString(0);
    var desc = reader.IsDBNull(1) ? "" : reader.GetString(1);
    var racialIds = reader.GetFieldValue<int[]>(2);

    return Results.Json(
        new { name, desc, racialids = racialIds },
        jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Get class by id (name, desc, racialids).");

app.MapGet("/stats", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, name, base_value FROM stats ORDER BY id",
        conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var list = new List<object>();
    while (await reader.ReadAsync())
    {
        list.Add(new
        {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            baseValue = reader.GetInt32(2),
        });
    }

    return Results.Json(list, jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("All stat definitions (base values).");

app.MapGet("/stats/{name}", async (string name, NpgsqlDataSource ds, CancellationToken cancellationToken) =>
{
    var stat = await GetStatByNameAsync(name, ds, cancellationToken);
    if (stat is null)
        return Results.NotFound();

    return Results.Json(
        new
        {
            stat.Id,
            stat.Name,
            baseValue = stat.BaseValue,
        },
        jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Preferred single-stat lookup by name (case-insensitive).");

app.MapGet("/charhealth", async (NpgsqlDataSource ds) =>
{
    var stat = await GetStatByNameAsync("charhealth", ds);
    if (stat is null)
        return Results.Json(new { name = "charhealth", baseValue = 0 }, jsonOptions);

    return Results.Json(
        new { name = stat.Name, baseValue = stat.BaseValue },
        jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Character health stat row (name charhealth).");

app.MapGet("/strength", async (NpgsqlDataSource ds) =>
{
    var stat = await GetStatByNameAsync("strength", ds);
    if (stat is null)
        return Results.Json(new { name = "strength", baseValue = 0 }, jsonOptions);

    return Results.Json(
        new { name = stat.Name, baseValue = stat.BaseValue },
        jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Strength stat row.");

app.MapGet("/abilities", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        """
        SELECT id, name, description, class_id
        FROM abilities
        ORDER BY id
        """,
        conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var list = new List<object>();
    while (await reader.ReadAsync())
    {
        list.Add(new
        {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            desc = reader.IsDBNull(2) ? "" : reader.GetString(2),
            class_id = reader.GetInt32(3),
        });
    }

    return Results.Json(list, jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Abilities (id, name, desc, class_id).");

app.MapGet("/race", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, name, modifiers FROM races ORDER BY id",
        conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var list = new List<object>();
    while (await reader.ReadAsync())
    {
        list.Add(new
        {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            modifiers = reader.IsDBNull(2) ? "" : reader.GetString(2),
        });
    }

    return Results.Json(list, jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("All races.");

app.MapGet("/premades", async (NpgsqlDataSource ds, int? class_id, int? race_id, string? q, int? limit, int? offset) =>
{
    if (!TryResolvePremadePaging(limit, offset, out var resolvedLimit, out var resolvedOffset))
        return Results.BadRequest("limit must be between 1 and 100, and offset must be 0 or greater.");

    var search = NormalizeSearch(q);

    await using var conn = await ds.OpenConnectionAsync();
    await using var countCmd = new NpgsqlCommand(
        """
        SELECT COUNT(*)
        FROM premades p
        WHERE (@class_id IS NULL OR p.class_id = @class_id)
          AND (@race_id IS NULL OR p.race_id = @race_id)
          AND (@q IS NULL OR p.name ILIKE '%' || @q || '%')
        """,
        conn);
    AddPremadeFilterParameters(countCmd, class_id, race_id, search);
    var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

    await using var cmd = new NpgsqlCommand(
        """
        SELECT p.id, p.name, p.class_id, p.race_id, c.name, r.name, p.stats::text
        FROM premades p
        INNER JOIN classes c ON c.id = p.class_id
        INNER JOIN races r ON r.id = p.race_id
        WHERE (@class_id IS NULL OR p.class_id = @class_id)
          AND (@race_id IS NULL OR p.race_id = @race_id)
          AND (@q IS NULL OR p.name ILIKE '%' || @q || '%')
        ORDER BY p.id
        LIMIT @limit
        OFFSET @offset
        """,
        conn);
    AddPremadeFilterParameters(cmd, class_id, race_id, search);
    cmd.Parameters.Add("@limit", NpgsqlDbType.Integer).Value = resolvedLimit;
    cmd.Parameters.Add("@offset", NpgsqlDbType.Integer).Value = resolvedOffset;
    await using var reader = await cmd.ExecuteReaderAsync();
    var list = new List<object>();
    while (await reader.ReadAsync())
    {
        var statsText = reader.GetString(6);
        var stats = JsonSerializer.Deserialize<JsonElement>(statsText);
        list.Add(new
        {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            class_id = reader.GetInt32(2),
            race_id = reader.GetInt32(3),
            @class = reader.GetString(4),
            race = reader.GetString(5),
            stats,
        });
    }

    return Results.Json(new
    {
        items = list,
        total,
        limit = resolvedLimit,
        offset = resolvedOffset,
    }, jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Premade characters with optional filters and pagination.");

app.MapGet("/names", async (NpgsqlDataSource ds, int? class_id, int? race_id, string? q) =>
{
    var search = NormalizeSearch(q);

    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        """
        SELECT p.name
        FROM premades p
        WHERE (@class_id IS NULL OR p.class_id = @class_id)
          AND (@race_id IS NULL OR p.race_id = @race_id)
          AND (@q IS NULL OR p.name ILIKE '%' || @q || '%')
        ORDER BY p.name
        """,
        conn);
    AddPremadeFilterParameters(cmd, class_id, race_id, search);
    await using var reader = await cmd.ExecuteReaderAsync();
    var names = new List<string>();
    while (await reader.ReadAsync())
        names.Add(reader.GetString(0));

    return Results.Json(names, jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Premade character names.");

app.MapPost("/generate", async (GenerateRequest? body, NpgsqlDataSource ds) =>
{
    if (body is null || body.ClassId < 1 || body.RaceId < 1)
        return Results.BadRequest();

    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        """
        SELECT
            c.id,
            c.name,
            c.description,
            r.id,
            r.name,
            r.modifiers
        FROM classes c
        INNER JOIN races r ON r.id = $2
        WHERE c.id = $1
          AND $2 = ANY (c.racial_ids)
        """,
        conn);
    cmd.Parameters.AddWithValue(body.ClassId);
    cmd.Parameters.AddWithValue(body.RaceId);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.BadRequest();

    var classId = reader.GetInt32(0);
    var className = reader.GetString(1);
    var classDesc = reader.IsDBNull(2) ? "" : reader.GetString(2);
    var raceId = reader.GetInt32(3);
    var raceName = reader.GetString(4);
    var raceModifiers = reader.IsDBNull(5) ? "" : reader.GetString(5);

    await reader.CloseAsync();
    await using var statsCmd = new NpgsqlCommand(
        "SELECT name, base_value FROM stats ORDER BY name",
        conn);
    await using var statsReader = await statsCmd.ExecuteReaderAsync();
    var statsDict = new Dictionary<string, int>();
    while (await statsReader.ReadAsync())
        statsDict[statsReader.GetString(0)] = statsReader.GetInt32(1);

    var sheet = new
    {
        classId,
        raceId,
        className,
        raceName,
        classDescription = classDesc,
        raceModifiers,
        stats = statsDict,
    };

    return Results.Json(sheet, jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Generate a character sheet for class_id + race_id (race must be allowed for class).")
    .Accepts<GenerateRequest>("application/json");

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program;

public sealed record StatRecord(int Id, string Name, int BaseValue);

public sealed record PremadeQuery(
    [property: JsonPropertyName("class_id")] int? ClassId,
    [property: JsonPropertyName("race_id")] int? RaceId,
    [property: JsonPropertyName("q")] string? Query,
    [property: JsonPropertyName("limit")] int? Limit,
    [property: JsonPropertyName("offset")] int? Offset);

public sealed record GenerateRequest(
    [property: JsonPropertyName("class_id")] int ClassId,
    [property: JsonPropertyName("race_id")] int RaceId);
