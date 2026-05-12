# SCRUM-68: Editable Character Sheets

| | |
|---|---|
| **Jira** | SCRUM-68 |
| **Author** | Kylee Down |
| **Date** | 2026-05-11 |
| **Status** | Approved |

---

## Summary

Allow logged-in players to edit a saved (or freshly generated) character sheet without regenerating the whole character. Editing happens inline on the existing sheet page — the layout is unchanged, fields simply become inputs.

---

## Section 1 — Visual / UX

### Read Mode (unchanged)

- `✏ Edit` button and `🖨 Print` button appear in the above-sheet bar above the `.sheet-wrap`
- Sheet content is entirely non-interactive display text

### Edit Mode (entered by clicking Edit)

**Toolbar changes:**
- Above-sheet bar swaps `✏ Edit` + `🖨 Print` → `Cancel` + `💾 Save`
- Print button is hidden in edit mode

**Edit-mode banner:**
- A narrow gold banner is injected at the top of `.sheet-wrap`:
  > _"✏ Edit Mode — unsaved changes will be lost if you navigate away"_
- Same gold (`#8b6914`) background as other accent elements; white text

**Editable fields — become inputs/textareas in-place (same font, same size):**

| Location | Field | Input type |
|---|---|---|
| Sheet header | Character Name | `<input>` (full-width, matches `char-name-display` font size) |
| Sheet header | Background | `<input>` |
| Sheet header | Player Name | `<input>` |
| Sheet header | Alignment | `<input>` |
| Ability column | STR, DEX, CON, INT, WIS, CHA score | `<input>` — borderless, underline only; modifier circle recalculates live |
| HP section | charhealth (Hit Point Maximum) | `<input>` — same borderless style |
| Traits column | Personality Traits, Ideals, Bonds, Flaws | `<textarea>` — same dimensions as the existing `.trait-content` box |

**Locked fields (read-only in edit mode):**
- Class & Level, Race, Experience Points
- Rendered at 60% opacity and italic — no inputs, no badges

**Validation:**
- Core stats (STR/DEX/CON/INT/WIS/CHA): integer, 1–30
- charhealth: integer, 1–999
- Character Name: required, non-blank
- On invalid value: input border turns red, small inline error appears below the field (e.g. "1–30")
- `💾 Save` button is **disabled** while any field has a validation error

**Live modifier recalculation:**
- Each stat `<input>` fires an `input` event
- Event handler recomputes `mod(value)` and updates the `⊕` modifier circle below the score

### Feedback states

| State | Presentation |
|---|---|
| Saving in progress | Save button shows "Saving…" and is disabled |
| Save success | Sheet exits edit mode; small green confirmation note appears below the sheet for 3 seconds then fades |
| Save failure | Stays in edit mode; red inline card appears near the save button: "✗ Save failed. Your edits are still here." |

### Cancel behaviour

- Re-renders the sheet from the last known-good in-memory `sheet` object (`populateSheet(sheet)`)
- Removes the edit-mode banner
- Restores the `Edit` + `Print` button row
- No partial edits are persisted

---

## Section 2 — Backend

### New endpoint: `PUT /api/character/sheets/{id}`

**Controller:** `CharacterSheetApiController` (`web-server/Controllers/MyCharactersController.cs`)

**Auth:** Cookie-based JWT, same as existing GET/DELETE endpoints. No token → 401.

**Ownership:** Sheet must belong to the requesting user. Wrong user or missing sheet → **404** (matches existing GET behaviour — does not leak existence).

**Request body — `UpdateSheetRequest`:**

```csharp
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
```

Class and race are intentionally absent — they are locked on the client and not updated by this endpoint.

**Validation:** `Name` required and non-blank → 400. Stats shape is trusted (client validates ranges).

**Response:**
- `200 { id, message: "Character updated" }` — success
- `400` — blank name
- `401` — no/invalid token
- `404` — sheet not found or wrong user

### New service method

Added to `ICharacterSheetService` and `CharacterSheetService`:

```csharp
Task<bool> UpdateSheetAsync(int sheetId, int userId, UpdateSheetRequest update);
```

Returns `false` if the sheet does not exist or belongs to a different user (controller maps this to 404).

**SQL:** `UPDATE character_sheets SET name=$3, background=$4, player_name=$5, alignment=$6, personality_traits=$7, ideals=$8, bonds=$9, flaws=$10, stats=$11::jsonb WHERE id=$1 AND user_id=$2`

> **Note:** The `character_sheets` table currently lacks the text columns `background`, `player_name`, `alignment`, `personality_traits`, `ideals`, `bonds`, `flaws`. A migration must be added to `web-server/database/schema/` (e.g. `003_add_sheet_text_fields.sql`) with nullable `TEXT` columns for each. This migration cascades through several layers:
>
> - **`SaveSheetRequest`** — add the seven text fields so `create.html` can persist them on first save (they default to empty string if not provided)
> - **`SaveSheetAsync`** — update the `INSERT` to write all seven columns
> - **`CharacterSheetDetail`** — add the seven text properties so they round-trip back when a saved sheet is loaded
> - **`GetSheetByIdAsync`** — update the `SELECT` to read the seven columns
> - **`my-characters.html` `viewSheet()`** — pass the seven text fields into `sheetData` so they appear in `sessionStorage` when navigating to the sheet page

### Frontend fixes required

**`my-characters.html`:** The `sheetData` object written to `sessionStorage` currently omits `id` and all seven text fields. Add `id: sheet.id` plus the text fields so the sheet page has the DB id and any previously saved text.

**`create.html`:** The auto-save POST fires and forgets. Capture the `{ id }` from the response and include `id` in the `sessionStorage` `sheetData` object so a freshly generated sheet also gets the Edit button immediately. The text fields will be empty on first generation (API does not generate personality/traits); the user fills them in via Edit mode.

---

## Section 3 — Frontend (`frontend/character-sheet.html`)

### State

```js
let editMode = false;
// sheet is the existing in-memory object loaded from sessionStorage
// sheet.id is present for saved sheets; undefined for sheets whose auto-save failed
```

### Edit button

Added to the existing `.above-sheet-bar` alongside the Print button.
- Visible and clickable only when `!editMode`
- Clicking calls `enterEditMode()`

### `enterEditMode()`

1. `editMode = true`
2. Swap button row: hide Edit + Print, show Cancel + Save
3. Inject edit banner into top of `.sheet-wrap`
4. For each editable display element: replace inner text with a pre-built `<input>` or `<textarea>` (identified by `data-field` attributes added to the display elements)
5. Attach `input` event listeners to stat fields for live modifier recalc and validation
6. Attach `input` event listener to Name field for blank validation

> **Note on stat `data-field` attributes:** The ability-score `<div class="ability-score">` elements are generated dynamically inside `buildAbilityCol()`. That function must be updated to add `data-field="stat-STR"` etc. to each score element when it renders them, so `enterEditMode()` can find and replace them.

### `exitEditMode()`

1. `editMode = false`
2. Call `populateSheet(sheet)` to re-render from last good state
3. Remove banner
4. Restore Edit + Print button row

### Save flow

1. Read all input values
2. Validate — if any error, show inline errors, keep edit mode, abort
3. Disable Save button, show "Saving…"
4. If `sheet.id` exists → `PUT /api/character/sheets/{id}`
5. If no `sheet.id` → `POST /api/character/sheets` (first save)
6. On success:
   - Update in-memory `sheet` object with new values
   - Update `sessionStorage`
   - If POST: store returned `id` into `sheet.id`
   - Call `exitEditMode()`
   - Show 3-second green confirmation
7. On failure:
   - Re-enable Save button
   - Show red inline failure card
   - Keep all edits intact

### `data-field` attribute map

| `data-field` value | Element type | Maps to `sheet` property |
|---|---|---|
| `name` | `#hdr-name` | `sheet.name` |
| `background` | `#hdr-background` | `sheet.background` |
| `playerName` | `#hdr-player` | `sheet.playerName` |
| `alignment` | `#hdr-alignment` | `sheet.alignment` |
| `stat-STR` … `stat-CHA` | `.ability-score` inside each `.ability-block` | `sheet.stats.STR` etc. |
| `stat-charhealth` | `#stat-hp-max` | `sheet.stats.charhealth` |
| `personalityTraits` | `#trait-personality` | `sheet.personalityTraits` |
| `ideals` | `#trait-ideals` | `sheet.ideals` |
| `bonds` | `#trait-bonds` | `sheet.bonds` |
| `flaws` | `#trait-flaws` | `sheet.flaws` |

---

## Section 4 — Tests (`web-server.Tests/EndpointTests.cs`)

### Factory changes

- Add `Mock<ICharacterSheetService> MockSheetService` to `LotrWebAppFactory`
- Register it the same way `MockAuthService` is registered (remove real, add mock)
- Default mock: `GetSheetByIdAsync` returns a sample `CharacterSheetDetail`; `UpdateSheetAsync` returns `true`; `SaveSheetAsync` returns `1`

### `GenerateTestToken` fix

The current helper encodes `ClaimTypes.NameIdentifier = "testuser"` (a string). `GetUserIdFromCookie()` calls `int.TryParse`, which fails on `"testuser"`. Change the claim value to `"1"` so user-id extraction works in all sheet tests.

### New tests

| Test name | Method | Path | Setup | Asserts |
|---|---|---|---|---|
| `UpdateSheet_WithAuth_Returns200` | PUT | `/api/character/sheets/1` | Mock returns `true` | 200 |
| `UpdateSheet_WithoutAuth_Returns401` | PUT | `/api/character/sheets/1` | No cookie | 401 |
| `UpdateSheet_WrongUser_Returns404` | PUT | `/api/character/sheets/1` | Mock returns `false` | 404 |
| `UpdateSheet_BlankName_Returns400` | PUT | `/api/character/sheets/1` | Body `Name = ""` | 400 |
| `GetSheet_WithAuth_Returns200` | GET | `/api/character/sheets/1` | Mock returns detail | 200 |
| `GetSheet_WithoutAuth_Returns401` | GET | `/api/character/sheets/1` | No cookie | 401 |
| `SaveSheet_WithAuth_Returns200` | POST | `/api/character/sheets` | Valid body | 200 |
| `SaveSheet_WithoutAuth_Returns401` | POST | `/api/character/sheets` | No cookie | 401 |

---

## Out of Scope

- Editing Class or Race (requires regeneration)
- Editing XP, Background tab fields (age, height, appearance, backstory, etc.), spell data
- Multiplayer / concurrent-edit conflict resolution
- Undo history beyond Cancel

---

## Non-editable fields — PR documentation note

The following fields are intentionally read-only in Edit mode and must be noted in the PR description:

- **Class & Level** — tied to character generation; changing requires a new `/generate` call
- **Race** — same as Class
- **Experience Points** — not part of the character sheet data model
- **Background tab** (age, height, weight, eyes, skin, hair, appearance, backstory, allies, treasure) — out of scope for this story; display-only for now