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

app.MapGet("/charhealth", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        """
        SELECT name, base_value
        FROM stats
        WHERE name = 'charhealth'
        """,
        conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.Json(new { name = "charhealth", baseValue = 0 }, jsonOptions);

    return Results.Json(
        new { name = reader.GetString(0), baseValue = reader.GetInt32(1) },
        jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Character health stat row (name charhealth).");

app.MapGet("/strength", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        """
        SELECT name, base_value
        FROM stats
        WHERE name = 'strength'
        """,
        conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.Json(new { name = "strength", baseValue = 0 }, jsonOptions);

    return Results.Json(
        new { name = reader.GetString(0), baseValue = reader.GetInt32(1) },
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

app.MapGet("/premades", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        """
        SELECT id, name, class_id, race_id, stats::text
        FROM premades
        ORDER BY id
        """,
        conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var list = new List<object>();
    while (await reader.ReadAsync())
    {
        var statsText = reader.GetString(4);
        var stats = JsonSerializer.Deserialize<JsonElement>(statsText);
        list.Add(new
        {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            class_id = reader.GetInt32(2),
            race_id = reader.GetInt32(3),
            stats,
        });
    }

    return Results.Json(list, jsonOptions);
})
    .WithTags("Game data")
    .WithSummary("Premade characters (class_id, race_id, stats JSON).");

app.MapGet("/names", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT name FROM premades ORDER BY name",
        conn);
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

public sealed record GenerateRequest(
    [property: JsonPropertyName("class_id")] int ClassId,
    [property: JsonPropertyName("race_id")] int RaceId);
