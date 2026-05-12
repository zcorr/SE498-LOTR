# Editable Character Sheets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let logged-in players edit name, stats, and text traits on a saved character sheet inline without regenerating the character.

**Architecture:** DB migration adds seven nullable text columns to `character_sheets`; a new `PUT /api/character/sheets/{id}` endpoint persists edits; `character-sheet.html` gains an inline Edit mode (same layout, inputs in place, gold banner, Save/Cancel buttons) backed by the new endpoint; `create.html` and `my-characters.html` are patched to propagate the sheet `id` through `sessionStorage` so the sheet page can call PUT.

**Tech Stack:** ASP.NET Core 10 (C#), Npgsql, xUnit + Moq, vanilla HTML/JS/Bootstrap 5

---

## File Map

| Action | Path | Change |
|--------|------|--------|
| Create | `web-server/database/schema/003_add_sheet_text_fields.sql` | ALTER TABLE migration |
| Modify | `web-server/Services/ICharacterSheetService.cs` | `UpdateSheetRequest`, new fields on existing DTOs, new interface method |
| Modify | `web-server/Services/CharacterSheetService.cs` | `UpdateSheetAsync`, updated `SaveSheetAsync` + `GetSheetByIdAsync` |
| Modify | `web-server/Controllers/MyCharactersController.cs` | `PUT /api/character/sheets/{id}` |
| Modify | `web-server.Tests/EndpointTests.cs` | Sheet service mock, token fix, 8 new tests |
| Modify | `frontend/my-characters.html` | Async detail fetch in `viewSheet()`, pass all fields |
| Modify | `frontend/create.html` | Capture POST `{ id }`, pass text fields in body |
| Modify | `frontend/character-sheet.html` | Edit mode UI, CSS, JS functions |

---

## Task 1: DB Migration

**Files:**
- Create: `web-server/database/schema/003_add_sheet_text_fields.sql`

- [ ] **Step 1: Create the migration file**

```sql
-- 003_add_sheet_text_fields.sql
ALTER TABLE character_sheets
  ADD COLUMN IF NOT EXISTS background        TEXT,
  ADD COLUMN IF NOT EXISTS player_name       TEXT,
  ADD COLUMN IF NOT EXISTS alignment         TEXT,
  ADD COLUMN IF NOT EXISTS personality_traits TEXT,
  ADD COLUMN IF NOT EXISTS ideals            TEXT,
  ADD COLUMN IF NOT EXISTS bonds             TEXT,
  ADD COLUMN IF NOT EXISTS flaws             TEXT;
```

- [ ] **Step 2: Apply migration (requires the Postgres container to be running)**

```bash
make db
```

Expected: no errors. Verify with:

```bash
make db-psql DB=lotr_users
```

Then in psql: `\d character_sheets` — confirm the seven new columns appear as nullable TEXT.

- [ ] **Step 3: Commit**

```bash
git add web-server/database/schema/003_add_sheet_text_fields.sql
git commit -m "feat(SCRUM-68): add text columns to character_sheets table"
```

---

## Task 2: Update Models and Interface

**Files:**
- Modify: `web-server/Services/ICharacterSheetService.cs`

- [ ] **Step 1: Replace the entire file contents**

Replace `web-server/Services/ICharacterSheetService.cs` with:

```csharp
namespace web_server.Services;

public interface ICharacterSheetService
{
    Task<int> SaveSheetAsync(int userId, SaveSheetRequest sheet);
    Task<List<CharacterSheetSummary>> GetSheetsForUserAsync(int userId);
    Task<CharacterSheetDetail?> GetSheetByIdAsync(int sheetId, int userId);
    Task<bool> UpdateSheetAsync(int sheetId, int userId, UpdateSheetRequest update);
    Task<bool> DeleteSheetAsync(int sheetId, int userId);
}

public class SaveSheetRequest
{
    public string Name { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string RaceName { get; set; } = string.Empty;
    public string ClassDescription { get; set; } = string.Empty;
    public string RaceModifiers { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
    public string PersonalityTraits { get; set; } = string.Empty;
    public string Ideals { get; set; } = string.Empty;
    public string Bonds { get; set; } = string.Empty;
    public string Flaws { get; set; } = string.Empty;
    public Dictionary<string, int> Stats { get; set; } = new();
}

public class UpdateSheetRequest
{
    public string Name { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
    public string PersonalityTraits { get; set; } = string.Empty;
    public string Ideals { get; set; } = string.Empty;
    public string Bonds { get; set; } = string.Empty;
    public string Flaws { get; set; } = string.Empty;
    public Dictionary<string, int> Stats { get; set; } = new();
}

public class CharacterSheetSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string RaceName { get; set; } = string.Empty;
    public Dictionary<string, int> Stats { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class CharacterSheetDetail : CharacterSheetSummary
{
    public string ClassDescription { get; set; } = string.Empty;
    public string RaceModifiers { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
    public string PersonalityTraits { get; set; } = string.Empty;
    public string Ideals { get; set; } = string.Empty;
    public string Bonds { get; set; } = string.Empty;
    public string Flaws { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Verify build**

```bash
cd "/Users/kyleedown/Documents/Chapman/2025-2026/Spring 26/SE Capstone/SE498-LOTR"
dotnet build web-server/web-server.csproj
```

Expected: build succeeds (will show compile error in CharacterSheetService.cs because `SaveSheetAsync` INSERT is now missing 7 columns — that is expected and is fixed in Task 4).

- [ ] **Step 3: Commit**

```bash
git add web-server/Services/ICharacterSheetService.cs
git commit -m "feat(SCRUM-68): add UpdateSheetRequest and text fields to sheet DTOs"
```

---

## Task 3: Write Failing Tests

**Files:**
- Modify: `web-server.Tests/EndpointTests.cs`

- [ ] **Step 1: Add `MockSheetService` property to `LotrWebAppFactory`**

In `EndpointTests.cs`, add the property declaration to `LotrWebAppFactory` right after the `MockJurassicAdsClient` declaration (around line 45):

```csharp
public Mock<ICharacterSheetService> MockSheetService { get; } = new();
```

- [ ] **Step 2: Add sheet service mock registration to `ConfigureWebHost`**

Inside `ConfigureWebHost`, add the following block immediately after the Jurassic mock block (after the `MockJurassicAdsClient.Setup(...)` call, around line 87):

```csharp
// ── Replace ICharacterSheetService with a mock ──
var sheetDescriptors = services
    .Where(d => d.ServiceType == typeof(ICharacterSheetService))
    .ToList();
foreach (var d in sheetDescriptors)
    services.Remove(d);
services.AddScoped<ICharacterSheetService>(_ => MockSheetService.Object);

MockSheetService
    .Setup(x => x.GetSheetsForUserAsync(It.IsAny<int>()))
    .ReturnsAsync(new List<CharacterSheetSummary>());

MockSheetService
    .Setup(x => x.SaveSheetAsync(It.IsAny<int>(), It.IsAny<SaveSheetRequest>()))
    .ReturnsAsync(1);

MockSheetService
    .Setup(x => x.GetSheetByIdAsync(It.IsAny<int>(), It.IsAny<int>()))
    .ReturnsAsync(new CharacterSheetDetail
    {
        Id = 1,
        Name = "Test Hero",
        ClassName = "Rogue",
        RaceName = "Human",
        Stats = new Dictionary<string, int> { ["STR"] = 10 },
    });

MockSheetService
    .Setup(x => x.UpdateSheetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<UpdateSheetRequest>()))
    .ReturnsAsync(true);

MockSheetService
    .Setup(x => x.DeleteSheetAsync(It.IsAny<int>(), It.IsAny<int>()))
    .ReturnsAsync(true);
```

- [ ] **Step 3: Fix `GenerateTestToken` — change `"testuser"` to `"1"`**

Find `GenerateTestToken()` (around line 196) and change both `"testuser"` string values to `"1"`:

```csharp
private static string GenerateTestToken()
{
    var key = Encoding.ASCII.GetBytes(
        "Cool_Mega_Secret_Key_For_JWT_Token_Generation");

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "1"),
        }),
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature),
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}
```

- [ ] **Step 4: Add the 8 new test methods**

Append these tests at the bottom of the `EndpointTests` class (before the final closing `}`):

```csharp
// =========================================================================
// CHARACTER SHEET CRUD ENDPOINTS
// =========================================================================

[Fact]
public async Task SaveSheet_WithAuth_Returns200()
{
    var client = CreateAuthenticatedClient();

    var response = await client.PostAsJsonAsync("/api/character/sheets", new
    {
        name = "Gandalf",
        className = "Wizard",
        raceName = "Maia",
        classDescription = "",
        raceModifiers = "",
        stats = new Dictionary<string, int> { ["STR"] = 10 },
    });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

[Fact]
public async Task SaveSheet_WithoutAuth_Returns401()
{
    var client = CreateUnauthenticatedClient();

    var response = await client.PostAsJsonAsync("/api/character/sheets", new
    {
        name = "Gandalf",
        className = "Wizard",
        raceName = "Maia",
        classDescription = "",
        raceModifiers = "",
        stats = new Dictionary<string, int> { ["STR"] = 10 },
    });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task GetSheet_WithAuth_Returns200()
{
    var client = CreateAuthenticatedClient();

    var response = await client.GetAsync("/api/character/sheets/1");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

[Fact]
public async Task GetSheet_WithoutAuth_Returns401()
{
    var client = CreateUnauthenticatedClient();

    var response = await client.GetAsync("/api/character/sheets/1");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task UpdateSheet_WithAuth_Returns200()
{
    var client = CreateAuthenticatedClient();

    var response = await client.PutAsJsonAsync("/api/character/sheets/1", new
    {
        name = "Aragorn",
        background = "Outlander",
        playerName = "Kylee",
        alignment = "Lawful Good",
        personalityTraits = "Stoic",
        ideals = "Duty",
        bonds = "Fellowship",
        flaws = "Stubborn",
        stats = new Dictionary<string, int> { ["STR"] = 16 },
    });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

[Fact]
public async Task UpdateSheet_WithoutAuth_Returns401()
{
    var client = CreateUnauthenticatedClient();

    var response = await client.PutAsJsonAsync("/api/character/sheets/1", new
    {
        name = "Aragorn",
        background = "",
        playerName = "",
        alignment = "",
        personalityTraits = "",
        ideals = "",
        bonds = "",
        flaws = "",
        stats = new Dictionary<string, int> { ["STR"] = 16 },
    });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task UpdateSheet_WrongUser_Returns404()
{
    // Override the default mock to simulate wrong-user (returns false)
    _factory.MockSheetService
        .Setup(x => x.UpdateSheetAsync(1, 1, It.IsAny<UpdateSheetRequest>()))
        .ReturnsAsync(false);

    var client = CreateAuthenticatedClient();

    var response = await client.PutAsJsonAsync("/api/character/sheets/1", new
    {
        name = "Aragorn",
        background = "",
        playerName = "",
        alignment = "",
        personalityTraits = "",
        ideals = "",
        bonds = "",
        flaws = "",
        stats = new Dictionary<string, int> { ["STR"] = 16 },
    });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}

[Fact]
public async Task UpdateSheet_BlankName_Returns400()
{
    var client = CreateAuthenticatedClient();

    var response = await client.PutAsJsonAsync("/api/character/sheets/1", new
    {
        name = "",
        background = "",
        playerName = "",
        alignment = "",
        personalityTraits = "",
        ideals = "",
        bonds = "",
        flaws = "",
        stats = new Dictionary<string, int> { ["STR"] = 16 },
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

- [ ] **Step 5: Run tests — confirm the split**

```bash
dotnet test web-server.Tests/web-server.Tests.csproj --logger "console;verbosity=normal" 2>&1 | tail -30
```

Expected outcome:
- `SaveSheet_WithAuth_Returns200` → **PASS** (endpoint exists, mock returns 1)
- `SaveSheet_WithoutAuth_Returns401` → **PASS**
- `GetSheet_WithAuth_Returns200` → **PASS** (endpoint exists, mock returns detail)
- `GetSheet_WithoutAuth_Returns401` → **PASS**
- `UpdateSheet_WithAuth_Returns200` → **FAIL** (405 Method Not Allowed — PUT endpoint not yet added)
- `UpdateSheet_WithoutAuth_Returns401` → **FAIL**
- `UpdateSheet_WrongUser_Returns404` → **FAIL**
- `UpdateSheet_BlankName_Returns400` → **FAIL**
- All previously passing tests → **PASS**

- [ ] **Step 6: Commit**

```bash
git add web-server.Tests/EndpointTests.cs
git commit -m "test(SCRUM-68): add sheet CRUD tests and sheet service mock"
```

---

## Task 4: Implement Service Methods

**Files:**
- Modify: `web-server/Services/CharacterSheetService.cs`

- [ ] **Step 1: Replace the file with the updated implementation**

Replace `web-server/Services/CharacterSheetService.cs` with:

```csharp
using System.Text.Json;
using Npgsql;

namespace web_server.Services;

public class CharacterSheetService : ICharacterSheetService
{
    private readonly NpgsqlDataSource _db;

    public CharacterSheetService(NpgsqlDataSource db)
    {
        _db = db;
    }

    public async Task<int> SaveSheetAsync(int userId, SaveSheetRequest sheet)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO character_sheets
              (user_id, name, class_name, race_name, class_description, race_modifiers,
               background, player_name, alignment, personality_traits, ideals, bonds, flaws, stats)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14::jsonb)
            RETURNING id
            """,
            conn);

        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(sheet.Name);
        cmd.Parameters.AddWithValue(sheet.ClassName);
        cmd.Parameters.AddWithValue(sheet.RaceName);
        cmd.Parameters.AddWithValue(sheet.ClassDescription);
        cmd.Parameters.AddWithValue(sheet.RaceModifiers);
        cmd.Parameters.AddWithValue(sheet.Background);
        cmd.Parameters.AddWithValue(sheet.PlayerName);
        cmd.Parameters.AddWithValue(sheet.Alignment);
        cmd.Parameters.AddWithValue(sheet.PersonalityTraits);
        cmd.Parameters.AddWithValue(sheet.Ideals);
        cmd.Parameters.AddWithValue(sheet.Bonds);
        cmd.Parameters.AddWithValue(sheet.Flaws);
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(sheet.Stats));

        var result = await cmd.ExecuteScalarAsync();
        return (int)result!;
    }

    public async Task<List<CharacterSheetSummary>> GetSheetsForUserAsync(int userId)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, name, class_name, race_name, stats::text, created_at
            FROM character_sheets
            WHERE user_id = $1
            ORDER BY created_at DESC
            """,
            conn);
        cmd.Parameters.AddWithValue(userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<CharacterSheetSummary>();
        while (await reader.ReadAsync())
        {
            var statsText = reader.GetString(4);
            var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(statsText)
                        ?? new Dictionary<string, int>();

            list.Add(new CharacterSheetSummary
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ClassName = reader.GetString(2),
                RaceName = reader.GetString(3),
                Stats = stats,
                CreatedAt = reader.GetDateTime(5),
            });
        }

        return list;
    }

    public async Task<CharacterSheetDetail?> GetSheetByIdAsync(int sheetId, int userId)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, name, class_name, race_name, class_description, race_modifiers,
                   background, player_name, alignment, personality_traits, ideals, bonds, flaws,
                   stats::text, created_at
            FROM character_sheets
            WHERE id = $1 AND user_id = $2
            """,
            conn);
        cmd.Parameters.AddWithValue(sheetId);
        cmd.Parameters.AddWithValue(userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var statsText = reader.GetString(13);
        var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(statsText)
                    ?? new Dictionary<string, int>();

        return new CharacterSheetDetail
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            ClassName = reader.GetString(2),
            RaceName = reader.GetString(3),
            ClassDescription = reader.IsDBNull(4) ? "" : reader.GetString(4),
            RaceModifiers = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Background = reader.IsDBNull(6) ? "" : reader.GetString(6),
            PlayerName = reader.IsDBNull(7) ? "" : reader.GetString(7),
            Alignment = reader.IsDBNull(8) ? "" : reader.GetString(8),
            PersonalityTraits = reader.IsDBNull(9) ? "" : reader.GetString(9),
            Ideals = reader.IsDBNull(10) ? "" : reader.GetString(10),
            Bonds = reader.IsDBNull(11) ? "" : reader.GetString(11),
            Flaws = reader.IsDBNull(12) ? "" : reader.GetString(12),
            Stats = stats,
            CreatedAt = reader.GetDateTime(14),
        };
    }

    public async Task<bool> UpdateSheetAsync(int sheetId, int userId, UpdateSheetRequest update)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE character_sheets
            SET name=$3, background=$4, player_name=$5, alignment=$6,
                personality_traits=$7, ideals=$8, bonds=$9, flaws=$10,
                stats=$11::jsonb
            WHERE id=$1 AND user_id=$2
            """,
            conn);

        cmd.Parameters.AddWithValue(sheetId);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(update.Name);
        cmd.Parameters.AddWithValue(update.Background);
        cmd.Parameters.AddWithValue(update.PlayerName);
        cmd.Parameters.AddWithValue(update.Alignment);
        cmd.Parameters.AddWithValue(update.PersonalityTraits);
        cmd.Parameters.AddWithValue(update.Ideals);
        cmd.Parameters.AddWithValue(update.Bonds);
        cmd.Parameters.AddWithValue(update.Flaws);
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(update.Stats));

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteSheetAsync(int sheetId, int userId)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM character_sheets WHERE id = $1 AND user_id = $2",
            conn);
        cmd.Parameters.AddWithValue(sheetId);
        cmd.Parameters.AddWithValue(userId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build web-server/web-server.csproj
```

Expected: build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add web-server/Services/CharacterSheetService.cs
git commit -m "feat(SCRUM-68): implement UpdateSheetAsync and update service for new columns"
```

---

## Task 5: Add PUT Endpoint and Pass All Tests

**Files:**
- Modify: `web-server/Controllers/MyCharactersController.cs`

- [ ] **Step 1: Add the PUT action to `CharacterSheetApiController`**

In `MyCharactersController.cs`, add the following method inside `CharacterSheetApiController`, after the `GetSheet` action and before `DeleteSheet`:

```csharp
// PUT /api/character/sheets/{id} — update a saved character sheet
[HttpPut("{id:int}")]
public async Task<IActionResult> UpdateSheet(int id, [FromBody] UpdateSheetRequest request)
{
    var userId = GetUserIdFromCookie();
    if (userId == null)
        return Unauthorized("No valid authentication token found");

    if (string.IsNullOrWhiteSpace(request.Name))
        return BadRequest("Character name is required");

    var updated = await _sheetService.UpdateSheetAsync(id, userId.Value, request);
    if (!updated)
        return NotFound();

    return Ok(new { id, message = "Character updated" });
}
```

- [ ] **Step 2: Run tests — all should pass**

```bash
dotnet test web-server.Tests/web-server.Tests.csproj --logger "console;verbosity=normal" 2>&1 | tail -20
```

Expected: all tests pass, including the four `UpdateSheet_*` tests.

- [ ] **Step 3: Commit**

```bash
git add web-server/Controllers/MyCharactersController.cs
git commit -m "feat(SCRUM-68): add PUT /api/character/sheets/{id} endpoint"
```

---

## Task 6: Fix `my-characters.html`

**Files:**
- Modify: `frontend/my-characters.html`

The current `viewSheet()` uses the summary data (no text fields, no id). Replace it to fetch the full detail from the API before navigating.

- [ ] **Step 1: Replace the `viewSheet` function**

Find the existing `viewSheet(sheet)` function (starts around line 299) and replace it entirely with:

```js
async function viewSheet(sheet) {
    // Fetch the full detail (includes text fields) before navigating
    let detail = sheet;
    try {
        const resp = await fetch(`${API_BASE}/character/sheets/${sheet.id}`);
        if (resp.ok) {
            detail = await resp.json();
        }
    } catch {
        // fall through — use summary data
    }

    const statMap = {
        strength: 'STR', dexterity: 'DEX', constitution: 'CON',
        intelligence: 'INT', wisdom: 'WIS', charisma: 'CHA',
    };

    const mappedStats = {};
    if (detail.stats) {
        for (const [key, value] of Object.entries(detail.stats)) {
            if (statMap[key]) {
                mappedStats[statMap[key]] = value;
            } else {
                mappedStats[key] = value;
            }
        }
    }

    const sheetData = {
        id: detail.id,
        name: detail.name,
        className: detail.className,
        raceName: detail.raceName,
        classDescription: detail.classDescription || '',
        raceModifiers: detail.raceModifiers || '',
        background: detail.background || '',
        playerName: detail.playerName || '',
        alignment: detail.alignment || '',
        personalityTraits: detail.personalityTraits || '',
        ideals: detail.ideals || '',
        bonds: detail.bonds || '',
        flaws: detail.flaws || '',
        stats: mappedStats,
    };

    sessionStorage.setItem('currentSheet', JSON.stringify(sheetData));
    window.location.href = '/character/sheet';
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/my-characters.html
git commit -m "feat(SCRUM-68): pass sheet id and text fields to character-sheet via sessionStorage"
```

---

## Task 7: Fix `create.html`

**Files:**
- Modify: `frontend/create.html`

The current auto-save fires and forgets. Capture the returned `{ id }` and include it (and text fields) in `sessionStorage`.

- [ ] **Step 1: Replace the auto-save block**

Find this block (around lines 423–441):

```js
        // Save to database so it persists for this user
        try {
          await fetch(`${API_BASE}/character/sheets`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              name: charName || '—',
              className: sheet.className,
              raceName: sheet.raceName,
              classDescription: sheet.classDescription || '',
              raceModifiers: sheet.raceModifiers || '',
              stats: sheet.stats,
            }),
          });
        } catch (saveErr) {
          console.warn('Could not save character to database:', saveErr);
        }

        sessionStorage.setItem('currentSheet', JSON.stringify(sheetData));
```

Replace it with:

```js
        // Save to database so it persists for this user; capture id for edit mode
        let savedId;
        try {
          const saveResp = await fetch(`${API_BASE}/character/sheets`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              name: charName || '—',
              className: sheet.className,
              raceName: sheet.raceName,
              classDescription: sheet.classDescription || '',
              raceModifiers: sheet.raceModifiers || '',
              background: '',
              playerName: '',
              alignment: '',
              personalityTraits: '',
              ideals: '',
              bonds: '',
              flaws: '',
              stats: sheet.stats,
            }),
          });
          if (saveResp.ok) {
            const saved = await saveResp.json();
            savedId = saved.id;
          }
        } catch (saveErr) {
          console.warn('Could not save character to database:', saveErr);
        }

        if (savedId) sheetData.id = savedId;
        sessionStorage.setItem('currentSheet', JSON.stringify(sheetData));
```

- [ ] **Step 2: Commit**

```bash
git add frontend/create.html
git commit -m "feat(SCRUM-68): capture sheet id from auto-save in create.html"
```

---

## Task 8: Edit Mode — Markup, CSS, and Core JS

**Files:**
- Modify: `frontend/character-sheet.html`

This task adds the Edit/Cancel/Save buttons, the CSS for edit mode, updates `buildAbilityCol()` to add IDs to score/mod elements, and implements `enterEditMode()` and `exitEditMode()`.

- [ ] **Step 1: Add CSS for edit mode**

Find the closing `</style>` tag (around line 558) and insert the following block just before it:

```css
    /* ── Edit mode ─────────────────────────────────────────── */
    .edit-banner {
      background-color: var(--lotr-accent);
      color: #fff8e7;
      font-size: 0.68rem;
      text-align: center;
      padding: 0.25rem 1rem;
      letter-spacing: 0.06em;
    }

    .edit-btn {
      background: none;
      border: 1px solid rgba(196,163,90,0.75);
      border-radius: 4px;
      color: rgba(196,163,90,0.9);
      font-size: 0.78rem;
      font-family: 'Georgia', serif;
      padding: 0.3rem 0.8rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .edit-btn:hover { border-color: #d4b870; color: #d4b870; }

    .cancel-btn {
      background: none;
      border: 1px solid rgba(196,163,90,0.45);
      border-radius: 4px;
      color: rgba(196,163,90,0.7);
      font-size: 0.78rem;
      font-family: 'Georgia', serif;
      padding: 0.3rem 0.8rem;
      cursor: pointer;
    }
    .cancel-btn:hover { border-color: rgba(196,163,90,0.7); color: rgba(196,163,90,0.9); }

    .save-btn {
      background-color: var(--lotr-accent);
      border: 1px solid var(--lotr-accent);
      border-radius: 4px;
      color: #fff8e7;
      font-size: 0.78rem;
      font-family: 'Georgia', serif;
      padding: 0.3rem 0.8rem;
      cursor: pointer;
      transition: background-color 0.2s;
    }
    .save-btn:hover { background-color: var(--lotr-accent-hover); }
    .save-btn:disabled { background-color: rgba(139,105,20,0.45); cursor: not-allowed; }

    .edit-input {
      background-color: var(--lotr-input-bg);
      border: 1px solid var(--lotr-input-border);
      border-radius: 3px;
      color: var(--lotr-text);
      font-family: 'Georgia', serif;
      padding: 0.1rem 0.3rem;
      width: 100%;
      box-sizing: border-box;
    }
    .edit-input.invalid { border-color: #c0392b; background-color: #fff0ee; color: #c0392b; }

    .edit-stat-input {
      background: transparent;
      border: none;
      border-bottom: 1.5px solid var(--lotr-card-border);
      color: var(--lotr-text);
      font-family: 'Georgia', serif;
      font-size: 1.5rem;
      font-weight: bold;
      text-align: center;
      width: 100%;
      padding: 0;
      outline: none;
    }
    .edit-stat-input.invalid { border-bottom-color: #c0392b; color: #c0392b; }

    .edit-textarea {
      background-color: var(--lotr-input-bg);
      border: 1px solid var(--lotr-input-border);
      border-radius: 3px;
      color: var(--lotr-text);
      font-family: 'Georgia', serif;
      font-size: 0.78rem;
      line-height: 1.4;
      padding: 0.2rem 0.3rem;
      resize: vertical;
      width: 100%;
      box-sizing: border-box;
      min-height: 40px;
    }

    .edit-error {
      color: #c0392b;
      font-size: 0.6rem;
      margin-top: 2px;
      display: block;
    }

    .field-locked { opacity: 0.6; font-style: italic; }

    .save-feedback {
      text-align: center;
      font-size: 0.75rem;
      padding: 0.35rem 1rem;
      margin: 0.4rem auto;
      max-width: 1100px;
      border-radius: 4px;
    }
    .save-feedback.success { background-color: #2d4a1e; color: #7ecb52; border: 1px solid #4a7a2e; }
    .save-feedback.failure { background-color: #4a1e1e; color: #e07070; border: 1px solid #c0392b; }
```

- [ ] **Step 2: Replace the above-sheet bar to add Edit/Cancel/Save buttons**

Find:

```html
  <div class="above-sheet-bar">
    <a href="javascript:history.back()" class="back-btn">← Back</a>
    <button class="print-btn" onclick="window.print()">🖨 Print</button>
  </div>
```

Replace with:

```html
  <div class="above-sheet-bar">
    <a href="javascript:history.back()" class="back-btn">← Back</a>
    <div style="display:flex;gap:0.5rem;align-items:center;">
      <button class="print-btn" id="print-btn" onclick="window.print()">🖨 Print</button>
      <button class="edit-btn"   id="edit-btn"   onclick="enterEditMode()">✏ Edit</button>
      <button class="cancel-btn" id="cancel-btn" style="display:none" onclick="exitEditMode()">Cancel</button>
      <button class="save-btn"   id="save-btn"   style="display:none" onclick="saveEdits()">💾 Save</button>
    </div>
  </div>
```

- [ ] **Step 3: Update `buildAbilityCol` to stamp IDs on score and mod elements**

Find the `buildAbilityCol` function and replace its inner template literal:

Find this string inside `buildAbilityCol`:

```js
      col.innerHTML += `
          <div class="ability-block">
            <div class="ability-name">${a.label.toUpperCase()}</div>
            <div class="ability-score">${score}</div>
            <div class="ability-mod">${modStr}</div>
          </div>`;
```

Replace with:

```js
      col.innerHTML += `
          <div class="ability-block">
            <div class="ability-name">${a.label.toUpperCase()}</div>
            <div class="ability-score" id="stat-score-${a.key}">${score}</div>
            <div class="ability-mod"   id="stat-mod-${a.key}">${modStr}</div>
          </div>`;
```

- [ ] **Step 4: Add `editMode` state variable**

Find this line near the top of the `<script>` block:

```js
  const sheet = normalizeSheetData(raw ? JSON.parse(raw) : DEV_PREVIEW);
```

Add directly below it:

```js
  let editMode = false;
```

- [ ] **Step 5: Add `enterEditMode` and `exitEditMode` functions**

Add these two functions inside the `<script>` block, just before the `populateSheet` function:

```js
  // ── Edit mode ─────────────────────────────────────────────
  function enterEditMode() {
    if (editMode) return;
    editMode = true;

    document.getElementById('edit-btn').style.display = 'none';
    document.getElementById('print-btn').style.display = 'none';
    document.getElementById('cancel-btn').style.display = 'inline-block';
    document.getElementById('save-btn').style.display = 'inline-block';

    const banner = document.createElement('div');
    banner.className = 'edit-banner';
    banner.id = 'edit-banner';
    banner.textContent = '✏ Edit Mode — unsaved changes will be lost if you navigate away';
    const sheetWrap = document.querySelector('.sheet-wrap');
    sheetWrap.insertBefore(banner, sheetWrap.firstChild);

    ['hdr-class', 'hdr-race', 'hdr-xp'].forEach(id => {
      const el = document.getElementById(id);
      if (el) el.classList.add('field-locked');
    });

    const headerFields = [
      { id: 'hdr-name',       field: 'name',       fontSize: '1.6rem', fontWeight: 'bold' },
      { id: 'hdr-background', field: 'background',  fontSize: '0.88rem' },
      { id: 'hdr-player',     field: 'playerName',  fontSize: '0.88rem' },
      { id: 'hdr-alignment',  field: 'alignment',   fontSize: '0.88rem' },
    ];
    headerFields.forEach(({ id, field, fontSize, fontWeight }) => {
      const el = document.getElementById(id);
      if (!el) return;
      const cur = sheet[field] ?? '';
      const input = document.createElement('input');
      input.type = 'text';
      input.className = 'edit-input';
      input.dataset.editField = field;
      input.value = (cur === '—' || !cur) ? '' : cur;
      input.style.fontSize = fontSize;
      if (fontWeight) input.style.fontWeight = fontWeight;
      input.addEventListener('input', validateEditInputs);
      el.textContent = '';
      el.appendChild(input);
    });

    ABILITIES.forEach(({ key }) => {
      const scoreEl = document.getElementById(`stat-score-${key}`);
      const modEl   = document.getElementById(`stat-mod-${key}`);
      if (!scoreEl) return;
      const val = sheet.stats?.[key] ?? 10;
      const input = document.createElement('input');
      input.type = 'number';
      input.className = 'edit-stat-input';
      input.dataset.editField = `stat-${key}`;
      input.dataset.statKey = key;
      input.value = val;
      input.addEventListener('input', () => {
        const v = parseInt(input.value);
        if (modEl && !isNaN(v) && v >= 1 && v <= 30) {
          modEl.textContent = mod(v);
        } else if (modEl) {
          modEl.textContent = '—';
        }
        validateEditInputs();
      });
      scoreEl.textContent = '';
      scoreEl.appendChild(input);
    });

    const hpEl = document.getElementById('stat-hp-max');
    if (hpEl) {
      const val = sheet.stats?.charhealth ?? 0;
      const input = document.createElement('input');
      input.type = 'number';
      input.className = 'edit-stat-input';
      input.style.fontSize = '0.9rem';
      input.style.fontWeight = 'bold';
      input.dataset.editField = 'stat-charhealth';
      input.value = val;
      input.addEventListener('input', validateEditInputs);
      hpEl.textContent = '';
      hpEl.appendChild(input);
    }

    const traitFields = [
      { id: 'trait-personality', field: 'personalityTraits' },
      { id: 'trait-ideals',      field: 'ideals' },
      { id: 'trait-bonds',       field: 'bonds' },
      { id: 'trait-flaws',       field: 'flaws' },
    ];
    traitFields.forEach(({ id, field }) => {
      const el = document.getElementById(id);
      if (!el) return;
      const val = sheet[field] ?? '';
      const textarea = document.createElement('textarea');
      textarea.className = 'edit-textarea';
      textarea.dataset.editField = field;
      textarea.value = (val === '—' || !val) ? '' : val;
      el.textContent = '';
      el.appendChild(textarea);
    });
  }

  function exitEditMode() {
    editMode = false;

    const banner = document.getElementById('edit-banner');
    if (banner) banner.remove();

    ['hdr-class', 'hdr-race', 'hdr-xp'].forEach(id => {
      const el = document.getElementById(id);
      if (el) el.classList.remove('field-locked');
    });

    document.getElementById('edit-btn').style.display   = 'inline-block';
    document.getElementById('print-btn').style.display  = 'inline-block';
    document.getElementById('cancel-btn').style.display = 'none';
    document.getElementById('save-btn').style.display   = 'none';

    const failure = document.getElementById('save-failure-msg');
    if (failure) failure.remove();

    populateSheet(sheet);
    loadClassAbilities(sheet);
  }
```

- [ ] **Step 6: Commit**

```bash
git add frontend/character-sheet.html
git commit -m "feat(SCRUM-68): add edit mode UI, CSS, enterEditMode, exitEditMode"
```

---

## Task 9: Edit Mode — Save Flow

**Files:**
- Modify: `frontend/character-sheet.html`

- [ ] **Step 1: Add `validateEditInputs`, `saveEdits`, and `showSaveFeedback`**

Add these three functions inside the `<script>` block, directly after `exitEditMode`:

```js
  function validateEditInputs() {
    let valid = true;

    const nameInput = document.querySelector('[data-edit-field="name"]');
    if (nameInput) {
      const errEl = nameInput.parentElement.querySelector('.edit-error');
      if (!nameInput.value.trim()) {
        nameInput.classList.add('invalid');
        if (!errEl) {
          const err = document.createElement('span');
          err.className = 'edit-error';
          err.textContent = 'Name is required';
          nameInput.parentElement.appendChild(err);
        }
        valid = false;
      } else {
        nameInput.classList.remove('invalid');
        if (errEl) errEl.remove();
      }
    }

    document.querySelectorAll('[data-stat-key]').forEach(input => {
      const v = parseInt(input.value);
      const errEl = input.parentElement.querySelector('.edit-error');
      if (isNaN(v) || v < 1 || v > 30) {
        input.classList.add('invalid');
        if (!errEl) {
          const err = document.createElement('span');
          err.className = 'edit-error';
          err.textContent = '1–30';
          input.parentElement.appendChild(err);
        }
        valid = false;
      } else {
        input.classList.remove('invalid');
        if (errEl) errEl.remove();
      }
    });

    const hpInput = document.querySelector('[data-edit-field="stat-charhealth"]');
    if (hpInput) {
      const v = parseInt(hpInput.value);
      const errEl = hpInput.parentElement.querySelector('.edit-error');
      if (isNaN(v) || v < 1 || v > 999) {
        hpInput.classList.add('invalid');
        if (!errEl) {
          const err = document.createElement('span');
          err.className = 'edit-error';
          err.textContent = '1–999';
          hpInput.parentElement.appendChild(err);
        }
        valid = false;
      } else {
        hpInput.classList.remove('invalid');
        if (errEl) errEl.remove();
      }
    }

    const saveBtn = document.getElementById('save-btn');
    if (saveBtn) saveBtn.disabled = !valid;

    return valid;
  }

  async function saveEdits() {
    if (!validateEditInputs()) return;

    const saveBtn = document.getElementById('save-btn');
    const origText = saveBtn.textContent;
    saveBtn.textContent = 'Saving…';
    saveBtn.disabled = true;

    const g = (field) => document.querySelector(`[data-edit-field="${field}"]`)?.value ?? '';

    const newStats = { ...sheet.stats };
    ABILITIES.forEach(({ key }) => {
      const input = document.querySelector(`[data-edit-field="stat-${key}"]`);
      if (input) newStats[key] = parseInt(input.value);
    });
    const hpInput = document.querySelector('[data-edit-field="stat-charhealth"]');
    if (hpInput) newStats.charhealth = parseInt(hpInput.value);

    const body = {
      name:             g('name').trim(),
      background:       g('background').trim(),
      playerName:       g('playerName').trim(),
      alignment:        g('alignment').trim(),
      personalityTraits: g('personalityTraits'),
      ideals:           g('ideals'),
      bonds:            g('bonds'),
      flaws:            g('flaws'),
      stats:            newStats,
    };

    try {
      let resp;
      if (sheet.id) {
        resp = await fetch(`/api/character/sheets/${sheet.id}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        });
      } else {
        resp = await fetch('/api/character/sheets', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            name:             body.name,
            className:        sheet.class ?? sheet.className ?? '',
            raceName:         sheet.race  ?? sheet.raceName  ?? '',
            classDescription: sheet.classDescription ?? '',
            raceModifiers:    sheet.raceModifiers ?? '',
            background:       body.background,
            playerName:       body.playerName,
            alignment:        body.alignment,
            personalityTraits: body.personalityTraits,
            ideals:           body.ideals,
            bonds:            body.bonds,
            flaws:            body.flaws,
            stats:            newStats,
          }),
        });
      }

      if (resp && resp.ok) {
        const result = await resp.json();
        sheet.name             = body.name;
        sheet.background       = body.background;
        sheet.playerName       = body.playerName;
        sheet.alignment        = body.alignment;
        sheet.personalityTraits = body.personalityTraits;
        sheet.ideals           = body.ideals;
        sheet.bonds            = body.bonds;
        sheet.flaws            = body.flaws;
        sheet.stats            = newStats;
        if (!sheet.id && result.id) sheet.id = result.id;
        sessionStorage.setItem('currentSheet', JSON.stringify(sheet));
        exitEditMode();
        showSaveFeedback(true);
      } else if (resp && resp.status === 401) {
        window.location.href = '/auth/login';
      } else {
        saveBtn.textContent = origText;
        saveBtn.disabled = false;
        showSaveFeedback(false);
      }
    } catch {
      saveBtn.textContent = origText;
      saveBtn.disabled = false;
      showSaveFeedback(false);
    }
  }

  function showSaveFeedback(success) {
    const existing = document.getElementById('save-feedback');
    if (existing) existing.remove();

    const existing2 = document.getElementById('save-failure-msg');
    if (existing2) existing2.remove();

    const el = document.createElement('div');
    el.className = 'save-feedback ' + (success ? 'success' : 'failure');
    el.textContent = success
      ? '✓ Changes saved successfully.'
      : '✗ Save failed. Your edits are still here.';

    if (success) {
      el.id = 'save-feedback';
      document.querySelector('.sheet-wrap').after(el);
      setTimeout(() => el.remove(), 3000);
    } else {
      el.id = 'save-failure-msg';
      const saveBtn = document.getElementById('save-btn');
      saveBtn.parentElement.appendChild(el);
    }
  }
```

- [ ] **Step 2: Run all tests to confirm backend is still green**

```bash
dotnet test web-server.Tests/web-server.Tests.csproj --logger "console;verbosity=normal" 2>&1 | tail -15
```

Expected: all tests pass.

- [ ] **Step 3: Manual smoke test** (requires `make` running)

```bash
make
```

1. Open http://localhost:5292/Auth/Login and log in as `admin` / `password`
2. Create a character — confirm the sheet page loads and the **✏ Edit** button appears
3. Click Edit — confirm the gold banner appears, stat inputs are visible, Print is hidden
4. Change the name and one stat to an invalid value (e.g. `99`) — confirm red border + "1–30" error, Save is disabled
5. Fix the value — confirm Save re-enables
6. Click Save — confirm the banner disappears, the updated name appears in the header, green confirmation fades after 3 seconds
7. Navigate to My Characters, click View Sheet — confirm the edited name and stat appear
8. Open Edit mode again, change something, click Cancel — confirm original values are restored
9. Open Edit mode, click Back in browser — values are not saved (no prompt needed, the banner warned)

- [ ] **Step 4: Final commit**

```bash
git add frontend/character-sheet.html
git commit -m "feat(SCRUM-68): add edit mode save flow with validation and feedback"
```