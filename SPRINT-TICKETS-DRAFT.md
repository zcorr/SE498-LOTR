# SE498-LOTR — Next Sprint Ticket Draft

Drafted from a review of the codebase as of April 20, 2026.

**Theme:** Close the gap between a working backend and a working product. Finish the half-built frontend flows, tighten the API so endpoints return scoped data instead of whole tables, and ship the first "user-facing" features (save, portraits, PDF export).

**Target project:** https://lotrbg.atlassian.net — SCRUM board.
**Status on creation:** To Do (all 10).

---

## Ticket 1 — Populate class and race dropdowns on the Create page from the API

**Type:** Story
**Labels:** frontend, create-flow, half-built

**User story**
As a player on the Create Character page, I want the class and race dropdowns to list the real options from the game database, so that I can pick a class and race that the generator will actually accept.

**Context**
`frontend/create.html` lines 175–176 have TODOs: "populate dynamically from GET /class" and "GET /race". The API endpoints `GET /class/{id}` and `GET /race` already exist and return the needed data. The web server proxy `CharacterController` already forwards to `/api/character/classes` and `/api/character/races`.

**Acceptance criteria**
- Given I open `/create` while authenticated, When the page loads, Then the Class `<select>` is populated from `GET /api/character/classes` (ids 1..4 today: Warrior, Istari, Burglar, Creature).
- Given the Class select is populated, When I pick a class, Then the Race `<select>` only shows races whose id appears in that class's `racialids` array.
- Given the API request fails, When the page loads, Then the dropdowns show a disabled "Unable to load — try again" option and the Generate button is disabled.
- No hardcoded class/race strings remain in `create.html`.

---

## Ticket 2 — Wire up the Generate button on the Create page

**Type:** Story
**Labels:** frontend, create-flow, half-built

**User story**
As a player on the Create Character page, I want the Generate button to call the backend and show me a generated sheet, so that I can actually produce a character without editing HTML myself.

**Context**
`frontend/create.html` lines ~287–297 have commented-out fetch logic with a "TODO: re-enable before going live" / "POST /generate endpoint not implemented" note — but `POST /generate` IS implemented on the API server (Program.cs:285) and proxied by `CharacterController` at `/api/character/generate`. The frontend just isn't wired.

**Acceptance criteria**
- Given class and race are selected, When I click Generate, Then the page POSTs `{class_id, race_id}` to `/api/character/generate` with the Bearer cookie.
- Given the API returns a sheet, When the response arrives, Then the page navigates to `/character-sheet` (or renders inline) showing class name, race name, class description, race modifiers, and all stats from the response.
- Given the API returns 400 (race not allowed for class), When the response arrives, Then a friendly error banner says "That race can't be a [class name]. Pick a different combination." and the form stays populated.
- Given the request takes > 2 s, When waiting, Then the Generate button shows a spinner and is disabled.

---

## Ticket 3 — Wire up the Premades gallery

**Type:** Story
**Labels:** frontend, premades, half-built

**User story**
As a player on the Premades page, I want to see the real premade characters (Gandalf, Frodo, Gollum…) rendered as selectable cards, so that I can pick one and generate its sheet without typing anything.

**Context**
`frontend/premades.html` line 274 has "TODO: GET /premades endpoint not implemented" but it IS implemented (Program.cs:238). Search bar and class/race filter pills exist in the markup but are non-functional. `PremadeController` proxies `/api/premade/list`.

**Acceptance criteria**
- Given I open `/premades` while authenticated, When the page loads, Then the grid renders one card per row returned from `GET /api/premade/list` showing name, class, race, and a portrait placeholder.
- Given I type in the search bar, When I enter text, Then cards filter client-side by name substring (case-insensitive).
- Given I click a class or race filter pill, When a filter is active, Then only cards matching that class/race are shown; clicking the pill again clears it.
- Given I click a premade card, When the click fires, Then the page navigates to `/character-sheet?premade_id={id}` and the sheet loads that premade's stats. (Server support for the `premade_id` fetch is covered by Ticket 9.)

---

## Ticket 4 — Replace hardcoded admin/password auth with a real user table

**Type:** Story
**Labels:** auth, backend, security, half-built

**User story**
As a player, I want to register an account and log in with my own username and password, so that my character sheets are tied to me and not to a shared "admin" login.

**Context**
`web-server/Services/AuthService.cs:20` hardcodes `username == "admin" && password == "password"`. No users table exists on the web server despite SPEC.md:144 calling for `Users (id, username, pw_hash, created_at)`. Web server has no Postgres connection configured.

**Acceptance criteria**
- Given a fresh environment, When the web server starts, Then a `users` table exists with columns `id`, `username UNIQUE`, `pw_hash`, `created_at`.
- Given I `POST /api/auth/register` with a username and password, When the handler runs, Then the password is hashed with BCrypt (work factor ≥ 10) and a user row is inserted; response is 201 with the user id.
- Given a registered user, When they POST `/api/auth/login` with correct credentials, Then `AuthService.AuthenticateUserAsync` looks up the user by username, verifies the hash, and returns a JWT.
- Given wrong credentials, When the login handler runs, Then it returns 401 with no information about whether the username exists.
- The string `admin` / `password` no longer appears in `AuthService.cs`.
- At least one integration test covers the register → login → token issued flow.

---

## Ticket 5 — Move JWT signing secret to configuration and add API-side Bearer validation

**Type:** Story
**Labels:** auth, backend, security, half-built

**User story**
As the team, I want the JWT signing secret read from configuration and validated on the API server, so that we are not shipping a known secret in source and the API stops trusting any request that reaches it.

**Context**
`AuthService.cs:23` and `:49` both hardcode `"your-secret-key-here-make-it-long"`. `web-server/Program.cs:22` has the same hardcoded value. The API server (`api-server/.../LotrApi/Program.cs`) has no auth middleware — any client can hit `/generate`, `/premades`, etc. The existing code comment at Program.cs line 12 explicitly calls this out as SCRUM-19.

**Acceptance criteria**
- The JWT secret is read from `Configuration["Jwt:SigningKey"]` (env var `Jwt__SigningKey` in production). Startup fails fast with a clear error if the key is missing or shorter than 32 bytes.
- `AuthService.cs` contains no literal secret string.
- The API server adds JWT Bearer auth middleware using the same signing key, and all endpoints except `GET /health` require a valid Bearer token.
- Given a request without a valid Bearer token, When it hits any game-data endpoint, Then the response is 401.
- Existing integration tests are updated to include the Bearer token and still pass.
- `appsettings.Development.json` contains a dev-only key; `appsettings.json` in production has no key and must be provided by env var.

---

## Ticket 6 — Scope `/abilities` and `/stats` to a class instead of returning everything

**Type:** Story
**Labels:** backend, api, scoping

**User story**
As the frontend, I want to fetch only the abilities and stats relevant to the class I'm looking at, so that the character sheet page isn't flooded with unrelated data and network payloads stay small.

**Context**
Today `GET /abilities` (Program.cs:187) returns every ability in the DB and `GET /stats` (:122) returns every stat definition. The character sheet only needs the abilities tied to one class and the stats relevant to one sheet. The `abilities` table already has `class_id`.

**Acceptance criteria**
- `GET /abilities` accepts an optional `?class_id={int}` query parameter. When provided, it returns only abilities whose `class_id` matches. When omitted, behavior is unchanged (full list).
- A new route `GET /class/{id}/abilities` returns the same scoped list; both routes share the same SQL path.
- Given a class id with no abilities, When the request runs, Then it returns an empty JSON array (not 404).
- Input validation: non-integer or negative `class_id` returns 400.
- Integration tests cover: (a) `?class_id=` matches expected abilities, (b) unknown class returns empty array, (c) omitted param returns the full list for backward compat.
- Web server `LotrApiClient.GetAbilitiesAsync` is updated to accept an optional class id parameter and the frontend's character sheet page uses it.

---

## Ticket 7 — Scope `/premades` and `/names` with filters and pagination

**Type:** Story
**Labels:** backend, api, scoping

**User story**
As the frontend premades gallery, I want to filter premades by class and race and page through them, so that the grid stays fast as the premade library grows.

**Context**
`GET /premades` (Program.cs:238) and `GET /names` (:269) return every row, unordered by relevance. The SPEC describes a growing library ("Future Considerations: Expanded character library across books and films"). Search UI on `premades.html` is already mocked.

**Acceptance criteria**
- `GET /premades` accepts optional query params: `class_id`, `race_id`, `q` (name substring, case-insensitive), `limit` (default 20, max 100), `offset` (default 0). The SQL applies each filter only when the param is present.
- Response shape: `{ "items": [...], "total": <count of matching rows>, "limit": <int>, "offset": <int> }`. The older flat-array shape is removed; `LotrApiClient.GetPremadesAsync` is updated.
- `GET /names` accepts the same filters (without pagination) and returns only premade names matching them.
- Given `limit=5&offset=5`, When three premades exist, Then `items` is empty and `total` is 3.
- Integration tests: filter by class_id, filter by race_id, search by q, pagination math, invalid limit (e.g. 0, 101) returns 400.
- Premades page (Ticket 3) is updated to pass the active filter pills as query params rather than filtering client-side, once this endpoint ships.

---

## Ticket 8 — Persist generated character sheets to the signed-in user's account

**Type:** Story
**Labels:** backend, web-server, new-feature

**User story**
As a logged-in player, I want to save a generated character sheet to my account and see my saved sheets on return visits, so that the tool is useful beyond a single session.

**Context**
SPEC.md:145 defines a `Sheets` table (`id, user_id, character_name, class_id, race_id, stats JSON, created_at`) but it's not implemented. `PremadeController.cs:71` has a stub `/api/premade/select` that acknowledges this: "in a full implementation, this would save the selected premade to the user's account." Depends on Ticket 4 (users table).

**Acceptance criteria**
- A `sheets` table exists with the schema above, keyed on `user_id` referencing `users.id`.
- `POST /api/character/sheets` saves a sheet for the current user. Body: `{ character_name, class_id, race_id, stats }`. Returns 201 with the new sheet id.
- `GET /api/character/sheets` returns the current user's saved sheets, most recent first.
- `GET /api/character/sheets/{id}` returns a specific sheet; 404 if not found or not owned by the current user (no information leak about ownership).
- `DELETE /api/character/sheets/{id}` removes the sheet; 404 under the same rules.
- Ticket 3's card click flow (or a Save button on the character sheet) calls `POST /api/character/sheets` and shows a "Saved" toast.
- Integration tests cover: save, list own only, 404 on other user's id, delete own.

---

## Ticket 9 — Add D&D-field tooltips and plain-language help on the character sheet

**Type:** Story
**Labels:** frontend, accessibility, new-feature

**User story**
As a Lord of the Rings fan who doesn't know D&D well, I want hover tooltips that explain what each stat, ability, and proficiency means in plain language, so that I can use the generated sheet without a rulebook.

**Context**
README lines 49–50 call this out as a core feature ("plain-language explanations for D&D fields", "Help Lord of the Rings fans understand how lore has been translated"). Nothing ships today.

**Acceptance criteria**
- A help-content JSON file (frontend-side or served by web server) maps each of: `strength`, `dexterity`, `constitution`, `intelligence`, `wisdom`, `charisma`, `charhealth`, and the generic concepts "class", "race", "ability", "modifier", "proficiency" to a 1–2 sentence plain-language explanation.
- Every matching stat name on `character-sheet.html`, `create.html`, and `premades.html` renders with a small `?` indicator. Hover (desktop) and tap (mobile) shows a Bootstrap tooltip/popover with the explanation.
- Tooltips are keyboard-accessible: focusing the indicator shows the explanation.
- Hover text does not overflow the viewport on a 375 px wide screen.
- A short LOTR-flavor note is included per stat (e.g. strength: "how hard you swing a sword — Boromir has high strength"). These live in the same JSON so content can be updated without code changes.

---

## Ticket 10 — Export a generated or saved character sheet as a PDF

**Type:** Story
**Labels:** frontend, new-feature

**User story**
As a DM preparing for a session, I want to export a character sheet as a PDF, so that I can print it or share it with my table outside the app.

**Context**
README line 44 lists this as a core Saving & Sharing feature. No export flow exists today. Depends loosely on Ticket 8 so saved sheets can be re-exported without regeneration.

**Acceptance criteria**
- A "Download PDF" button is visible on `character-sheet.html` when a sheet is loaded (either freshly generated or fetched by id).
- Clicking the button produces a one-page PDF containing: character name, class, race, class description, race modifiers, full stats table, abilities list, and a small lore/flavor block.
- The PDF is generated server-side by a new endpoint `GET /api/character/sheets/{id}/pdf` (uses an existing library — QuestPDF, iText, or headless Chromium via a Razor view — pick one and justify in the PR). For freshly generated but not yet saved sheets, an alternative `POST /api/character/pdf` takes the sheet payload.
- The PDF is returned with `Content-Type: application/pdf` and a filename like `aragorn-warrior-human.pdf`.
- Integration test: POST a sheet payload and assert the response is a non-empty PDF (magic bytes `%PDF-`).
- The button is hidden while a sheet is being generated and re-enables only when one is loaded.

---

## Sprint summary

| # | Ticket | Pillar |
|---|--------|--------|
| 1 | Populate class/race dropdowns | Finish half-built |
| 2 | Wire up Generate button | Finish half-built |
| 3 | Wire up Premades gallery | Finish half-built |
| 4 | Real user table + register/login | Finish half-built + security |
| 5 | JWT secret to config + API Bearer validation | Finish half-built + security |
| 6 | Scope `/abilities` by class | Fix API over-returning |
| 7 | Scope `/premades` with filters + pagination | Fix API over-returning |
| 8 | Save sheets to user account | New feature |
| 9 | D&D-field tooltips | New feature |
| 10 | PDF export | New feature |

**Suggested order:** 5 → 4 → 1 → 2 → 6 → 3 → 7 → 8 → 9 → 10. (Security foundations first so the later features inherit real auth.)
