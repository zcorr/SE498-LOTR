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

On page load, three fetches run in parallel via `Promise.all`:

| Fetch | Endpoint | Used for |
|---|---|---|
| Premades | `GET /api/premade/list` | Card data |
| Classes | `GET /api/character/classes` | Class pills + card display |
| Races | `GET /api/character/races` | Race pills + card display |

Two lookup maps are built from the results:
- `classMap`: `{ id → name }` — used to resolve `class_id` to a display name on each card
- `raceMap`: `{ id → name }` — used to resolve `race_id` to a display name on each card

Filter state:
- `activeClassId`: `number | null` — `null` means "All"
- `activeRaceId`: `number | null` — `null` means "All"
- `searchQuery`: `string` — trimmed, lowercased

All filtering is done in-memory in `renderCards()`. No extra network calls per keystroke or pill click.

---

## Frontend Changes (`frontend/premades.html`)

### HTML

Remove all hardcoded class filter pills. Replace with two empty containers:

```html
<div class="filter-bar" id="class-filter-bar"></div>
<div class="filter-bar" id="race-filter-bar"></div>
```

### JavaScript

**`init()`** — replaces `fetchPremades()`. Runs three fetches in `Promise.all`. On failure, shows error state. On success, builds lookup maps and pills, then calls `renderCards()`.

**`buildPills(containerId, items, activeId, onSelect)`** — generic pill builder. Renders an "All" pill plus one pill per item from the API. Wires click → update state variable → `renderCards()`.

**`renderCards()`** — updated filter logic:
- Search: `c.name.toLowerCase().includes(searchQuery)` (case-insensitive)
- Class filter: `activeClassId === null || c.class_id === activeClassId`
- Race filter: `activeRaceId === null || c.race_id === activeRaceId`
- Card display: resolves names via `classMap[c.class_id] ?? 'Unknown'` and `raceMap[c.race_id] ?? 'Unknown'`

**Placeholder data removed.** If the API fails, we show an error state rather than silently falling back to fake data.

### Visible States

| State | Behaviour |
|---|---|
| Loading | `#status-msg` visible, grid hidden |
| Loaded (results) | Grid visible, `#status-msg` hidden |
| Empty (no matches) | Grid visible with "No characters found" message |
| Error | `#status-msg` shows error text in `--lotr-error` color |

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
// const res = await fetch(`/api/premade/list?${params}`);
// Then move this fetch call into renderCards() so it re-fetches on each filter change.
const res = await fetch('/api/premade/list');
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

### C# tests to add (`web-server.Tests/EndpointTests.cs`)

These live alongside the existing xUnit endpoint tests and use the same `LotrWebAppFactory` mock pattern.

| Test | Mock setup | Assert |
|---|---|---|
| `GetPremades_WithAuth_ReturnsCorrectShape` | Mock returns 3 premades with `Id`, `Name`, `Class_id`, `Race_id`, `Stats` | 200 + JSON array with correct fields |
| `GetPremades_WithAuth_EmptyList_Returns200AndEmptyArray` | Mock returns empty list | 200 + `[]` |

### What still needs a JS test framework (Jest or Playwright)

The following acceptance criteria from SCRUM-58 **cannot be tested** with the existing C# xUnit setup because the filter logic runs in client-side JavaScript:

- Search-only filtering (typing in the search box narrows cards by name)
- Class-only filtering (clicking a class pill shows only that class)
- Race-only filtering (clicking a race pill shows only that race)
- Combined filtering (search + class + race applied together)
- Empty results state (all filters active with no matches shows the empty message)
- Loading state visible before data arrives
- Error state visible when API call fails

**To add these tests**, set up one of:
- **Jest** — unit-test `renderCards()` logic in isolation with fixture data (fastest, no browser needed)
- **Playwright** — spin up the full app and drive the browser (highest confidence, tests real DOM)

Until that tooling is added, these scenarios should be manually verified against the running app. Add a tracking comment near `renderCards()` in the HTML.