// =============================================================================
// LOTR API — implementation checklist (you fill in; tests are written first/TDD)
// =============================================================================
//
// SCRUM-11 (skeleton): Expose a process-liveness route so ops/CI can ping the API
// without conflicting with SPEC "GET /health" (character health data, SCRUM-23).
// Suggested: GET /live -> 200 OK (body optional). Jira SCRUM-11 mentions /health
// for "app is up"; either rename that AC to /live or split routes with your team.
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
//   GET  /health       -> character health values (NOT process liveness)
//   GET  /strength     -> strength values
//   GET  /abilities    -> abilities list (id, name, desc, class_id)
//   GET  /race         -> races (id, name, modifiers)
//   GET  /species      -> species (id, name, traits)
//   GET  /premades     -> premade sheets incl. class_id, race_id, stats JSON
//   GET  /names        -> available character name strings
//   POST /generate     -> body: class_id, race_id; returns full generated sheet
//
// NFR: keep handlers under ~1s (SPEC).
//
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddScoped<I????, ?????>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// TODO: builder.Services — DbContext, auth, options, any domain services.

var app = builder.Build();

// TODO: Middleware pipeline — HTTPS, authentication/authorization, etc.

// TODO: MapGet / MapPost for each SPEC route (or MapControllers if you switch style).

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program;
