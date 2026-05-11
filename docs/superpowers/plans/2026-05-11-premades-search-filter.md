# Premades Search & Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the non-functional search bar and filter pills on the Premades page, and add C# endpoint-shape tests.

**Architecture:** Three fetches on page load (premades required; classes and races degrade gracefully). Filter state is two integers (`activeClassId`, `activeRaceId`) plus a search string, all filtered client-side in `renderCards()`. Filter pills are built dynamically from the API responses.

**Tech Stack:** Vanilla JS (no framework), ASP.NET Core 10, xUnit + Moq (C# tests), `dotnet test`

**Spec:** `docs/superpowers/specs/2026-05-11-premades-search-filter-design.md`

---

## File Map

| File | Change |
|---|---|
| `frontend/premades.html` | Replace hardcoded filter pills with empty containers; replace broken JS filter logic with `init()` / `buildPills()` / `renderCards()` |
| `web-server.Tests/EndpointTests.cs` | Remove stale comment; add empty-list test; add `PremadeShapeFactory` + `PremadeShapeTests` |

---

## Task 1: Remove stale bug comment from existing tests

**Files:**
- Modify: `web-server.Tests/EndpointTests.cs` (around line 393)

The comment at lines 393–396 says `GET /api/character/classes` "has a bug — it calls `GetAbilitiesAsync` instead of `GetClassesAsync`". This bug is already fixed in `CharacterController.cs` (line 73 calls `GetClassesAsync`). The comment is misleading.

- [ ] **Step 1: Delete the stale comment**

In `web-server.Tests/EndpointTests.cs`, find the `GetClasses_WithAuth_Returns200` test. It currently reads:

```csharp
[Fact]
public async Task GetClasses_WithAuth_Returns200()
{
    var client = CreateAuthenticatedClient();

    var response = await client.GetAsync("/api/character/classes");

    // Note: this endpoint currently has a bug — it calls
    // GetAbilitiesAsync instead of a proper GetClassesAsync.
    // The test still passes because the mock returns data either way.
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

Replace it with:

```csharp
[Fact]
public async Task GetClasses_WithAuth_Returns200()
{
    var client = CreateAuthenticatedClient();

    var response = await client.GetAsync("/api/character/classes");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

- [ ] **Step 2: Run tests to confirm nothing broke**

```bash
cd "web-server.Tests" && dotnet test --verbosity normal
```

Expected: all existing tests pass (same count as before).

- [ ] **Step 3: Commit**

```bash
git add web-server.Tests/EndpointTests.cs
git commit -m "test(SCRUM-58): remove stale bug comment from GetClasses test"
```

---

## Task 2: Add empty-list test for premades endpoint

**Files:**
- Modify: `web-server.Tests/EndpointTests.cs` (inside the existing `EndpointTests` class, after `SelectPremade_WithoutAuth_Returns401`)

The existing `LotrWebAppFactory` already configures `GetPremadesAsync` to return `new List<PremadeDTO>()`. This test verifies the endpoint returns `200` with an empty JSON array when no premades exist.

- [ ] **Step 1: Add the test**

Add the following method inside the `EndpointTests` class, after the `SelectPremade_WithoutAuth_Returns401` test:

```csharp
[Fact]
public async Task GetPremades_WithAuth_EmptyList_Returns200AndEmptyArray()
{
    var client = CreateAuthenticatedClient();

    var response = await client.GetAsync("/api/premade/list");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = (await response.Content.ReadAsStringAsync()).Trim();
    Assert.Equal("[]", body);
}
```

- [ ] **Step 2: Run tests to confirm it passes**

```bash
cd "web-server.Tests" && dotnet test --verbosity normal
```

Expected: all tests pass, including the new one.

- [ ] **Step 3: Commit**

```bash
git add web-server.Tests/EndpointTests.cs
git commit -m "test(SCRUM-58): add empty-list assertion for GET /api/premade/list"
```

---

## Task 3: Add PremadeShapeFactory and shape test

**Files:**
- Modify: `web-server.Tests/EndpointTests.cs` — add `PremadeShapeFactory` class and `PremadeShapeTests` class at the bottom of the file

This test verifies that when the premades endpoint returns data, the JSON sent to the browser contains the fields the frontend relies on: `id`, `name`, `class_id`, `race_id`, `stats`. We assert on the raw HTTP response JSON (Option B) to avoid constructing `JsonElement` directly and to test what the browser actually receives.

A separate factory is needed because the shared `LotrWebAppFactory` returns an empty premade list by default. Mutating its mock in a single test would pollute other tests in the shared class.

- [ ] **Step 1: Add `using System.Text.Json;` at the top of the file**

In `web-server.Tests/EndpointTests.cs`, add to the existing using block at the top:

```csharp
using System.Text.Json;
```

- [ ] **Step 2: Add `PremadeShapeFactory` at the bottom of the file**

Append the following after the closing `}` of the `EndpointTests` class (but still inside the `namespace web_server.Tests;` scope):

```csharp
// =============================================================================
// PREMADE SHAPE FACTORY
// Separate factory so we can seed specific premade data without affecting
// the shared LotrWebAppFactory used by EndpointTests.
// =============================================================================
public class PremadeShapeFactory : WebApplicationFactory<Program>
{
    public Mock<ILotrApiClient> MockApiClient { get; } = new();
    public Mock<IAuthService> MockAuthService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var apiDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ILotrApiClient));
            if (apiDescriptor != null)
                services.Remove(apiDescriptor);

            var httpDescriptors = services
                .Where(d => d.ServiceType == typeof(IHttpClientFactory)
                         || (d.ImplementationType != null
                             && d.ImplementationType == typeof(LotrApiClient)))
                .ToList();
            foreach (var d in httpDescriptors)
                services.Remove(d);

            var authDescriptors = services
                .Where(d => d.ServiceType == typeof(IAuthService))
                .ToList();
            foreach (var d in authDescriptors)
                services.Remove(d);

            services.AddSingleton<ILotrApiClient>(MockApiClient.Object);
            services.AddScoped<IAuthService>(_ => MockAuthService.Object);

            MockAuthService
                .Setup(x => x.SeedDefaultUserAsync())
                .Returns(Task.CompletedTask);
            MockAuthService
                .Setup(x => x.ValidateToken(It.IsAny<string>()))
                .Returns(true);

            var statsElement = JsonSerializer.Deserialize<JsonElement>("{\"STR\":16,\"DEX\":14}");

            MockApiClient
                .Setup(x => x.GetPremadesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<PremadeDTO>
                {
                    new() { Id = 1, Name = "Aragorn", Class_id = 1, Race_id = 1, Stats = statsElement },
                    new() { Id = 2, Name = "Gandalf", Class_id = 2, Race_id = 2, Stats = statsElement },
                    new() { Id = 3, Name = "Legolas", Class_id = 3, Race_id = 3, Stats = statsElement },
                });
        });
    }
}
```

- [ ] **Step 3: Add `PremadeShapeTests` class**

Append after `PremadeShapeFactory`:

```csharp
// =============================================================================
// PREMADE SHAPE TESTS
// Verifies that GET /api/premade/list returns the JSON fields the frontend needs.
// Asserts on the raw HTTP response body (not a constructed DTO) to test exactly
// what the browser receives, and to avoid JsonElement construction complexity.
// =============================================================================
public class PremadeShapeTests : IClassFixture<PremadeShapeFactory>
{
    private readonly PremadeShapeFactory _factory;

    public PremadeShapeTests(PremadeShapeFactory factory)
    {
        _factory = factory;
    }

    private static string GenerateTestToken()
    {
        var key = Encoding.ASCII.GetBytes(
            "Cool_Mega_Secret_Key_For_JWT_Token_Generation");
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "testuser"),
                new Claim(ClaimTypes.Name, "testuser"),
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature),
        };
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("Cookie", $"AuthToken={GenerateTestToken()}");
        return client;
    }

    [Fact]
    public async Task GetPremades_WithAuth_ReturnsCorrectShape()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/premade/list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(3, arr.GetArrayLength());

        var first = arr[0];
        Assert.True(first.TryGetProperty("id", out _),      "missing 'id'");
        Assert.True(first.TryGetProperty("name", out _),    "missing 'name'");
        Assert.True(first.TryGetProperty("class_id", out _),"missing 'class_id'");
        Assert.True(first.TryGetProperty("race_id", out _), "missing 'race_id'");
        Assert.True(first.TryGetProperty("stats", out _),   "missing 'stats'");
    }
}
```

- [ ] **Step 4: Run tests to confirm both new tests pass**

```bash
cd "web-server.Tests" && dotnet test --verbosity normal
```

Expected: all tests pass. The new `GetPremades_WithAuth_ReturnsCorrectShape` test passes because the endpoint already exists and the DTO fields are already serialized correctly.

- [ ] **Step 5: Commit**

```bash
git add web-server.Tests/EndpointTests.cs
git commit -m "test(SCRUM-58): add PremadeShapeFactory and premade endpoint shape tests"
```

---

## Task 4: Fix premades.html — HTML containers

**Files:**
- Modify: `frontend/premades.html`

Replace the hardcoded filter pills with two empty dynamic containers. Keeping this as its own commit makes the HTML change reviewable separately from the JS rewrite.

- [ ] **Step 1: Replace the hardcoded filter-bar block**

In `frontend/premades.html`, find and replace the entire `<!-- Filters -->` section (lines ~243–251):

**Remove this:**
```html
  <!-- Filters -->
  <div class="filter-bar">
    <span class="filter-label">Class:</span>
    <button class="filter-pill active" data-filter-type="class" data-filter-value="">All</button>
    <button class="filter-pill" data-filter-type="class" data-filter-value="barbarian">Barbarian</button>
    <button class="filter-pill" data-filter-type="class" data-filter-value="cleric">Cleric</button>
    <button class="filter-pill" data-filter-type="class" data-filter-value="rogue">Rogue</button>
    <button class="filter-pill" data-filter-type="class" data-filter-value="sorcerer">Sorcerer</button>
    <button class="filter-pill" data-filter-type="class" data-filter-value="wizard">Wizard</button>
  </div>
```

**Replace with:**
```html
  <!-- Filters — populated dynamically by JS from /api/character/classes and /api/character/races -->
  <div class="filter-bar" id="class-filter-bar"></div>
  <div class="filter-bar" id="race-filter-bar"></div>
```

- [ ] **Step 2: Commit the HTML-only change**

```bash
git add frontend/premades.html
git commit -m "fix(SCRUM-58): replace hardcoded filter pills with dynamic containers"
```

---

## Task 5: Fix premades.html — JavaScript rewrite

**Files:**
- Modify: `frontend/premades.html` (the `<script>` block)

Replace the entire `<script>` block (from `<script>` to `</script>`) with the following. This is the complete new script — do not merge with the old one.

- [ ] **Step 1: Replace the entire `<script>` block**

Find the opening `<script>` tag after the Bootstrap bundle script tag and replace everything from `<script>` to `</script>` with:

```html
<script>
  // ── Config ────────────────────────────────────────────────
  const API_BASE = 'http://localhost:5292/api';

  // ── State ─────────────────────────────────────────────────
  let allCharacters = [];
  let classMap = {};  // { id: name }
  let raceMap  = {};  // { id: name }
  let activeClassId = null;  // null = "All"
  let activeRaceId  = null;  // null = "All"
  let searchQuery   = '';

  // ── Build filter pills ────────────────────────────────────
  // Renders an "All" pill + one pill per item into containerId.
  // Calls setActive(id|null) then re-renders on each click.
  // If items is empty the container stays blank (no orphaned label).
  function buildPills(containerId, items, label, setActive) {
    const container = document.getElementById(containerId);
    container.innerHTML = '';
    if (items.length === 0) return;

    const labelSpan = document.createElement('span');
    labelSpan.className = 'filter-label';
    labelSpan.textContent = label;
    container.appendChild(labelSpan);

    function activatePill(targetPill) {
      container.querySelectorAll('.filter-pill').forEach(p => p.classList.remove('active'));
      targetPill.classList.add('active');
    }

    const allPill = document.createElement('button');
    allPill.className = 'filter-pill active';
    allPill.textContent = 'All';
    allPill.addEventListener('click', () => {
      setActive(null);
      activatePill(allPill);
      renderCards();
    });
    container.appendChild(allPill);

    items.forEach(item => {
      const pill = document.createElement('button');
      pill.className = 'filter-pill';
      pill.textContent = item.name;
      pill.addEventListener('click', () => {
        setActive(item.id);
        activatePill(pill);
        renderCards();
      });
      container.appendChild(pill);
    });
  }

  // ── Render cards ──────────────────────────────────────────
  // TODO(SCRUM-XX): add JS filter tests (Jest/Playwright) for search-only,
  // class-only, race-only, combined, and empty results scenarios.
  function renderCards() {
    const grid   = document.getElementById('card-grid');
    const status = document.getElementById('status-msg');

    const filtered = allCharacters.filter(c => {
      const matchSearch = !searchQuery || c.name.toLowerCase().includes(searchQuery);
      const matchClass  = activeClassId === null || c.class_id === activeClassId;
      const matchRace   = activeRaceId  === null || c.race_id  === activeRaceId;
      return matchSearch && matchClass && matchRace;
    });

    grid.innerHTML = '';

    if (filtered.length === 0) {
      grid.innerHTML = '<div class="no-results">No characters found matching your search.</div>';
    } else {
      filtered.forEach(c => {
        const stats     = c.stats || {};
        const className = classMap[c.class_id] ?? 'Unknown';
        const raceName  = raceMap[c.race_id]   ?? 'Unknown';
        const card = document.createElement('div');
        card.className = 'char-card';
        card.innerHTML = `
          <div class="char-name">${c.name}</div>
          <div class="char-meta">${className} · ${raceName}</div>
          <div class="stat-row">
            ${Object.entries(stats).map(([k, v]) =>
              `<span class="stat-badge"><strong>${k}</strong> ${v}</span>`
            ).join('')}
          </div>
          <button class="view-btn">View Sheet</button>
        `;
        card.querySelector('.view-btn').addEventListener('click', () => selectCharacter(c));
        grid.appendChild(card);
      });
    }

    status.style.display = 'none';
    grid.style.display   = 'grid';
  }

  // ── Select character → go to sheet ───────────────────────
  function selectCharacter(character) {
    sessionStorage.setItem('currentSheet', JSON.stringify(character));
    window.location.href = '/character/sheet';
  }

  // ── Init ──────────────────────────────────────────────────
  async function init() {
    const status = document.getElementById('status-msg');
    const grid   = document.getElementById('card-grid');

    status.textContent    = 'Loading characters…';
    status.style.display  = 'block';
    status.style.color    = '';
    grid.style.display    = 'none';

    // Premades are required — show error and stop if unavailable.
    try {
      // SCRUM-49: When /api/premade/list supports query params, replace the line below with:
      // const params = new URLSearchParams();
      // if (searchQuery) params.set('q', searchQuery);
      // if (activeClassId !== null) params.set('class_id', activeClassId);
      // if (activeRaceId  !== null) params.set('race_id',  activeRaceId);
      // const res = await fetch(`${API_BASE}/premade/list?${params}`);
      // Then move this fetch call into renderCards() so it re-fetches on each filter change.
      const res = await fetch(`${API_BASE}/premade/list`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      allCharacters = await res.json();
    } catch {
      status.textContent = 'Could not load characters. Please try again later.';
      status.style.color = 'var(--lotr-error)';
      return;
    }

    // Classes and races degrade gracefully — a failed fetch just omits that filter row.
    const [classResult, raceResult] = await Promise.allSettled([
      fetch(`${API_BASE}/character/classes`).then(r => r.ok ? r.json() : Promise.reject(r.status)),
      fetch(`${API_BASE}/character/races`).then(r   => r.ok ? r.json() : Promise.reject(r.status)),
    ]);

    const classes = classResult.status === 'fulfilled' ? classResult.value : [];
    const races   = raceResult.status  === 'fulfilled' ? raceResult.value  : [];

    classes.forEach(c => { classMap[c.id] = c.name; });
    races.forEach(r   => { raceMap[r.id]  = r.name; });

    buildPills('class-filter-bar', classes, 'Class:', v => { activeClassId = v; });
    buildPills('race-filter-bar',  races,   'Race:',  v => { activeRaceId  = v; });

    renderCards();
  }

  // ── Search input ──────────────────────────────────────────
  document.getElementById('search-input').addEventListener('input', e => {
    searchQuery = e.target.value.toLowerCase().trim();
    renderCards();
  });

  // ── Logout ────────────────────────────────────────────────
  document.getElementById('logout-link').addEventListener('click', e => {
    e.preventDefault();
    window.location.href = '/auth/login';
  });

  init();
</script>
```

- [ ] **Step 2: Run the C# tests to confirm nothing server-side broke**

```bash
cd "web-server.Tests" && dotnet test --verbosity normal
```

Expected: all tests pass.

- [ ] **Step 3: Manual verification checklist**

Start both servers (`make run` or run API server + web server separately), then:

1. Navigate to `/premade` — confirm "Loading characters…" appears then cards render.
2. Type a name fragment in the search box (e.g. `ara`) — confirm only matching cards remain visible.
3. Click a class pill — confirm only cards of that class remain; pill turns gold/active.
4. Click "All" — confirm all cards return.
5. Click a race pill — confirm only cards of that race remain.
6. Combine search + class + race — confirm all three filters apply together.
7. Apply a filter that matches nothing — confirm "No characters found matching your search." appears.
8. Click "View Sheet" on a card — confirm navigation to `/character/sheet`.
9. Stop the API server, reload — confirm the error message appears instead of an infinite spinner.
10. If you can simulate a classes-fetch failure (e.g., temporarily break the `/api/character/classes` route), confirm cards still load and only the class filter row is absent.

- [ ] **Step 4: Commit**

```bash
git add frontend/premades.html
git commit -m "fix(SCRUM-58): wire up search and dynamic class/race filters on premades page"
```

---

## Self-Review Checklist

- [x] **Spec coverage:** Problem (root causes) ✓ | Dynamic pills ✓ | `classMap`/`raceMap` lookups ✓ | `renderCards()` filter logic ✓ | Graceful degradation ✓ | Loading/empty/error states ✓ | SCRUM-49 migration comment ✓ | C# shape test (Option B raw JSON) ✓ | Empty-list test ✓ | Stale comment removed ✓ | JS test TODO comment ✓
- [x] **Placeholder scan:** No TBD/TODO in implementation steps. SCRUM-49 comment is intentional.
- [x] **Type consistency:** `activeClassId`/`activeRaceId` are `null | number` throughout. `buildPills` signature (`containerId, items, label, setActive`) is consistent across definition and call sites. `classMap`/`raceMap` keys are `id` (number) — `c.class_id` and `c.race_id` from the API are numbers — consistent. `PremadeShapeFactory` uses `Class_id`/`Race_id` (matching `PremadeDTO`). `GetPremadesAsync(It.IsAny<string>())` matches `ILotrApiClient` signature.