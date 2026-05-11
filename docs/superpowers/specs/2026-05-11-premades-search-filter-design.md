# Premades Search & Filter — Design Spec (SCRUM-58)

**Date:** 2026-05-11
**Branch:** feature/Bug-58

---

## Problem

The Premades page has a search bar and class filter pills, but neither does anything when the real API is used. The root causes are:

1. The API returns `class_id` (integer) and `race_id` (integer), but the filter logic compares `c.class` and `c.race` — string fields that only exist in the JS placeholder data, not in real API responses.
2. Filter pills use hardcoded string values (`"barbarian"`) that can never match an integer `class_id`.
3. There are no race filter pills at all.
4. The fallback placeholder data masked the bug during development.

---

## Approach: Hybrid Client-Side Filtering (Query-Param Ready)

Client-side filtering using integer IDs now. Structured so the switch to server-side filtering (SCRUM-49) is a minimal change. See **SCRUM-49 Migration** section below.

---

## Architecture & Data Flow

On page load, three fetches run via two separate `Promise.all` groups — premades are required; classes and races degrade gracefully:

```
Step 1 — required: fetch premades (fail hard if this fails → error state)
Step 2 — optional: Promise.all([fetchClasses, fetchRaces])
         If either fails, that filter row is simply omitted from the UI.
         Cards still render using classMap[id] ?? 'Unknown' and raceMap[id] ?? 'Unknown'.
```

| Fetch | Endpoint | Failure behaviour |
|---|---|---|
| Premades | `GET /api/premade/list` | Show error state, stop |
| Classes | `GET /api/character/classes` | Omit class filter row, show cards |
| Races | `GET /api/character/races` | Omit race filter row, show cards |

Two lookup maps are built from the results:
- `classMap`: `{ id → name }` — used to resolve `class_id` to a display name on each card
- `raceMap`: `{ id → name }` — used to resolve `race_id` to a display name on each card

Filter state:
- `activeClassId`: `number | null` — `null` means "All"
- `activeRaceId`: `number | null` — `null` means "All"
- `searchQuery`: `string` — trimmed, lowercased

All filtering is done in-memory in `renderCards()`. No extra network calls per keystroke or pill click.

### URL convention

All three fetches use the existing hardcoded `API_BASE = 'http://localhost:5292/api'`:
- `${API_BASE}/premade/list`
- `${API_BASE}/character/classes`
- `${API_BASE}/character/races`

This matches the existing pattern in the file and avoids mixing relative and absolute URLs.

---

## Frontend Changes (`frontend/premades.html`)

### HTML

Remove all hardcoded class filter pills and their `.filter-label` span. Replace with two empty containers:

```html
<div class="filter-bar" id="class-filter-bar"></div>
<div class="filter-bar" id="race-filter-bar"></div>
```

### JavaScript

**`init()`** — replaces `fetchPremades()`. Fetches premades first; on failure shows error state and stops. Then fetches classes and races in `Promise.allSettled` so a failure in either only omits that filter row rather than breaking the page. Builds lookup maps and pills, then calls `renderCards()`.

**`buildPills(containerId, items, getActive, setActive)`** — generic pill builder. Renders an "All" pill plus one pill per item from the API. Wires click → update state variable → `renderCards()`. If `items` is empty or the fetch failed, the container stays empty (no row shown).

**`renderCards()`** — updated filter logic:
- Search: `c.name.toLowerCase().includes(searchQuery)` (case-insensitive)
- Class filter: `activeClassId === null || c.class_id === activeClassId`
- Race filter: `activeRaceId === null || c.race_id === activeRaceId`
- Card display: resolves names via `classMap[c.class_id] ?? 'Unknown'` and `raceMap[c.race_id] ?? 'Unknown'`

**Placeholder data removed.** If the premades fetch fails, we show an error state. Filter rows degrade gracefully as described above.

### Visible States

| State | Behaviour |
|---|---|
| Loading | `#status-msg` visible, grid hidden |
| Loaded (results) | Grid visible, `#status-msg` hidden |
| Empty (no matches) | Grid visible with "No characters found" message |
| Error (premades fetch failed) | `#status-msg` shows error text in `--lotr-error` color |
| Partial (classes or races fetch failed) | Cards and search work; affected filter row simply absent |

---

## SCRUM-49 Migration

When the backend `/premades` endpoint gains support for `q`, `class_id`, and `race_id` query params, the client-side filtering can be replaced with server-side filtering.

### Frontend change (the one-line swap in `init()`)

Current code (client-side):
```js
// SCRUM-49: When /api/premade/list supports query params, replace the line below with:
// const params = new URLSearchParams();
// if (searchQuery) params.set('q', searchQuery);
// if (activeClassId !== null) params.set('class_id', activeClassId);
// if (activeRaceId !== null) params.set('race_id', activeRaceId);
// const res = await fetch(`${API_BASE}/premade/list?${params}`);
// Then move this fetch call into renderCards() so it re-fetches on each filter change.
const res = await fetch(`${API_BASE}/premade/list`);
```

### Web server changes also needed (SCRUM-49)

`web-server/Controllers/PremadeController.cs` — update `GetPremades()` to accept and forward query params:
```csharp
[HttpGet("list")]
public async Task<IActionResult> GetPremades(
    [FromQuery] string? q,
    [FromQuery] int? class_id,
    [FromQuery] int? race_id)
```

`web-server/Services/ILotrApiClient.cs` — update signature:
```csharp
Task<List<PremadeDTO>> GetPremadesAsync(string bearerToken, string? q = null, int? classId = null, int? raceId = null);
```

`web-server/Services/LotrApiClient.cs` — build query string and pass to API:
```csharp
var qs = new QueryString();
if (!string.IsNullOrWhiteSpace(q)) qs = qs.Add("q", q);
if (classId.HasValue) qs = qs.Add("class_id", classId.Value.ToString());
if (raceId.HasValue) qs = qs.Add("race_id", raceId.Value.ToString());
var response = await _httpClient.GetAsync($"/premades{qs.Value}");
```

---

## Testing

### C# tests to add

New tests go in a **separate test class** (`PremadeShapeTests`) with its own `LotrWebAppFactory` instance so mock data can be configured without affecting the existing `EndpointTests` shared factory.

The `PremadeShapeTests` factory configures `GetPremadesAsync` to return a list of three `PremadeDTO` objects. Because `PremadeDTO.Stats` is `System.Text.Json.JsonElement` (a struct that cannot be constructed with `new`), tests assert on the raw HTTP response JSON rather than on a constructed DTO — this tests what the browser actually receives and sidesteps the JsonElement construction problem:

```csharp
var json = await response.Content.ReadAsStringAsync();
var doc = JsonDocument.Parse(json);
Assert.True(doc.RootElement[0].TryGetProperty("stats", out _));
```

| Test class | Test | Mock setup | Assert |
|---|---|---|---|
| `PremadeShapeTests` | `GetPremades_WithAuth_ReturnsCorrectShape` | 3 premades with all fields populated | 200 + response JSON contains `id`, `name`, `class_id`, `race_id`, `stats` |
| `PremadeShapeTests` | `GetPremades_WithAuth_EmptyList_Returns200AndEmptyArray` | Empty list | 200 + response body is `[]` |
| `EndpointTests` (existing) | Remove stale bug comment from `GetClasses_WithAuth_Returns200` | — | The bug it described (`GetAbilitiesAsync` instead of `GetClassesAsync`) is already fixed in `CharacterController.cs`; the comment is misleading and should be deleted |

### What still needs a JS test framework (Jest or Playwright)

The following acceptance criteria from SCRUM-58 **cannot be tested** with the existing C# xUnit setup because the filter logic runs in client-side JavaScript:

- Search-only filtering (typing in the search box narrows cards by name)
- Class-only filtering (clicking a class pill shows only that class)
- Race-only filtering (clicking a race pill shows only that race)
- Combined filtering (search + class + race applied together)
- Empty results state (all filters active with no matches shows the empty message)
- Loading state visible before data arrives
- Error state visible when premades API call fails
- Graceful degradation when classes or races fetch fails

**To add these tests**, set up one of:
- **Jest** — unit-test `renderCards()` logic in isolation with fixture data (fastest, no browser needed)
- **Playwright** — spin up the full app and drive the browser (highest confidence, tests real DOM)

Until that tooling is added, these scenarios should be manually verified against the running app. A `// TODO(SCRUM-XX): add JS filter tests` comment is placed near `renderCards()` in the HTML to track this.