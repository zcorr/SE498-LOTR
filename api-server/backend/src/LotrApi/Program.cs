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
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

var jwtSecret = builder.Configuration["Jwt:Secret"];

if(string.IsNullOrWhiteSpace(jwtSecret)) {
	jwtSecret = "Cool_Mega_Secret_Key_For_JWT_Token_Generation";
}

var key  = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
	options.RequireHttpsMetadata = false;
	options.TokenValidationParameters = new TokenValidationParameters{
		ValidateIssuerSigningKey = true,
		IssuerSigningKey = new SymmetricSecurityKey(key),
		ValidateIssuer = false,
		ValidateAudience = false,
		ValidateLifetime = true,
		ClockSkew = TimeSpan.Zero
	};
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

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
    .WithSummary("Get class by id (name, desc, racialids).")
	.RequireAuthorization();

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
    .WithSummary("All stat definitions (base values).")
	.RequireAuthorization();

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
    .WithSummary("Get a single stat definition by name (case-insensitive).")
	.RequireAuthorization();

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
    .WithSummary("Character health stat row (name charhealth).")
	.RequireAuthorization();

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
    .WithSummary("Strength stat row.")
	.RequireAuthorization();

app.MapGet("/abilities", async (string? class_id, NpgsqlDataSource ds) =>
{
    if (class_id is null)
        return await Program.GetAbilitiesResultAsync(ds, jsonOptions, null);

    if (!int.TryParse(class_id, out var parsedClassId) || parsedClassId < 0)
        return Results.BadRequest(new { error = "class_id must be a non-negative integer." });

    return await Program.GetAbilitiesResultAsync(ds, jsonOptions, parsedClassId);
})
    .WithTags("Game data")
    .WithSummary("Abilities (id, name, desc, class_id), optionally filtered by class_id.")
	.RequireAuthorization();

app.MapGet("/class/{id:int}/abilities", async (int id, NpgsqlDataSource ds) =>
{
    if (id < 0)
        return Results.BadRequest(new { error = "id must be a non-negative integer." });

    return await Program.GetAbilitiesResultAsync(ds, jsonOptions, id);
})
    .WithTags("Game data")
    .WithSummary("Abilities for a specific class id.")
	.RequireAuthorization();

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
    .WithSummary("All races.")
	.RequireAuthorization();

app.MapGet("/classes", async (NpgsqlDataSource ds) =>
    {
        await using var conn = await ds.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, description, racial_ids FROM classes ORDER BY id",
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
                racialids = reader.GetFieldValue<int[]>(3),          
            });
        }

        return Results.Json(list, jsonOptions);
    })
    .WithTags("Game data")
    .WithSummary("All classes.")
    .RequireAuthorization();

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
    .WithSummary("Premade characters (class_id, race_id, stats JSON).")
	.RequireAuthorization();

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
    .WithSummary("Premade character names.")
	.RequireAuthorization();

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

// ── Roll stats: 4d6 drop lowest, per standard D&D rules ──
    var random = new Random();

    int Roll4d6DropLowest()
    {
        var rolls = new int[4];
        for (int i = 0; i < 4; i++)
            rolls[i] = random.Next(1, 7); // 1-6 inclusive
        Array.Sort(rolls);
        return rolls[1] + rolls[2] + rolls[3]; // drop lowest (index 0)
    }

// Base rolled stats
    var statNames = new[] { "strength", "dexterity", "constitution",
        "intelligence", "wisdom", "charisma" };
    var statsDict = new Dictionary<string, int>();
    foreach (var name in statNames)
        statsDict[name] = Roll4d6DropLowest();

// ── Apply race modifiers ──
    if (!string.IsNullOrWhiteSpace(raceModifiers))
    {
        try
        {
            var modifiers = JsonSerializer.Deserialize<Dictionary<string, int>>(raceModifiers);
            if (modifiers != null)
            {
                foreach (var (stat, bonus) in modifiers)
                {
                    if (statsDict.ContainsKey(stat))
                        statsDict[stat] += bonus;
                }
            }
        }
        catch
        {
            // If modifiers aren't valid JSON, skip them
        }
    }

// ── Get charhealth from the stats table ──
    await using var healthCmd = new NpgsqlCommand(
        "SELECT base_value FROM stats WHERE lower(name) = 'charhealth'",
        conn);
    var healthResult = await healthCmd.ExecuteScalarAsync();
    var baseHealth = healthResult is int h ? h : 20;

// Add charhealth with CON modifier bonus
    var conModifier = (statsDict["constitution"] - 10) / 2;
    statsDict["charhealth"] = baseHealth + conModifier;
    
    
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
    .Accepts<GenerateRequest>("application/json")
	.RequireAuthorization();

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program;

public partial class Program
{
    internal static async Task<IResult> GetAbilitiesResultAsync(
        NpgsqlDataSource ds,
        JsonSerializerOptions jsonOptions,
        int? classId)
    {
        await using var conn = await ds.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, name, description, class_id
            FROM abilities
            WHERE (@class_id IS NULL OR class_id = @class_id)
            ORDER BY id
            """,
            conn);
        cmd.Parameters.Add(new NpgsqlParameter<int?>("class_id", NpgsqlDbType.Integer)
        {
            TypedValue = classId,
        });

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
    }
}

public sealed record GenerateRequest(
    [property: JsonPropertyName("class_id")] int ClassId,
    [property: JsonPropertyName("race_id")] int RaceId);

file sealed record StatRecord(int Id, string Name, int BaseValue);
