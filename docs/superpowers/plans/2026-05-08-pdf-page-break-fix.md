# PDF Page Break Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the `@media print` CSS in `character-sheet.html` so the character sheet always fits on a single A4 landscape page across Chrome, Firefox, and Safari.

**Architecture:** All changes are confined to the `@media print` block in `frontend/character-sheet.html`. Fix A corrects five dead CSS rules (wrong class names) and adds overflow/break-inside controls. Fix B adds `zoom: 0.92` to `.sheet-wrap` as a cross-browser safety margin.

**Tech Stack:** HTML/CSS, browser `window.print()` → Save as PDF

---

### Task 1: Fix `.sheet-wrap` overflow and `.tab-panel.active` break control

**Files:**
- Modify: `frontend/character-sheet.html` (lines 519–549, `@media print` block)

The current `@media print` block has two structural gaps: `.sheet-wrap` keeps `overflow: hidden` from screen styles (never reset for print), and `.tab-panel.active` has no `break-inside: avoid`, allowing the browser to split the sheet mid-element across pages.

- [ ] **Step 1: Open `frontend/character-sheet.html` and locate the `@media print` block**

  It starts around line 520 with `@page { size: A4 landscape; margin: 0.6cm; }`.

- [ ] **Step 2: Update the `.sheet-wrap` rule inside `@media print`**

  Find this existing rule:
  ```css
  .sheet-wrap {
    max-width: 100%;
    width: 100%;
    box-shadow: none;
    border: 2px solid #8b6914;
    background-color: #f5ead6 !important;
  }
  ```

  Replace it with:
  ```css
  .sheet-wrap {
    max-width: 100%;
    width: 100%;
    box-shadow: none;
    border: 2px solid #8b6914;
    background-color: #f5ead6 !important;
    overflow: visible !important;
    zoom: 0.92;
  }
  ```

- [ ] **Step 3: Update `.tab-panel.active` inside `@media print`**

  Find:
  ```css
  .tab-panel.active { display: block !important; padding: 0.6rem 0.75rem; }
  ```

  Replace with:
  ```css
  .tab-panel.active { display: block !important; padding: 0.6rem 0.75rem; break-inside: avoid; }
  ```

- [ ] **Step 4: Save the file**

---

### Task 2: Replace the five dead CSS rules with correct class names

**Files:**
- Modify: `frontend/character-sheet.html` (bottom of `@media print` block)

The print block currently ends with these five rules that target class names that do not exist in the HTML — so none of them ever fired:

```css
.stat-block { gap: 0.35rem; }
.stat-box { padding: 0.25rem 0.3rem; }
.stat-score { font-size: 1.1rem; }
.section-body { gap: 0.3rem; }
.combat-grid { gap: 0.35rem; }
```

- [ ] **Step 1: Delete those five dead rules from the `@media print` block**

  Remove all five lines shown above.

- [ ] **Step 2: Add the corrected rules in their place**

  Insert these immediately before the closing `}` of `@media print`:
  ```css
  .ability-col { gap: 0.35rem; }
  .ability-block { padding: 0.25rem 0.3rem; }
  .ability-score { font-size: 1.1rem; }
  .sheet-body { gap: 0.5rem; }
  .combat-top { gap: 0.35rem; }
  ```

- [ ] **Step 3: Save the file**

---

### Task 3: Add spacing tightening for the tall skills/saves column

**Files:**
- Modify: `frontend/character-sheet.html` (`@media print` block)

The skills column has 18 skill rows + 6 saving throw rows = 24 stacked `.check-item` elements. Each item's `line-height` and padding accumulates across 24 rows and is the most likely element to push the sheet onto a second page when spacing is not tightened.

- [ ] **Step 1: Add tightening rules for `.check-item` and `.check-list`**

  Append inside `@media print` (before the closing `}`):
  ```css
  .check-item { line-height: 1.2; padding: 0.05rem 0; }
  .check-list { padding: 0.4rem 0.5rem; }
  ```

- [ ] **Step 2: Add tightening rules for the four layout columns**

  Append inside `@media print`:
  ```css
  .skills-col { gap: 0.4rem; }
  .combat-col { gap: 0.4rem; }
  .traits-col { gap: 0.35rem; }
  .trait-box { padding: 0.3rem 0.4rem; }
  ```

- [ ] **Step 3: Verify the complete final `@media print` block looks exactly like this**

  ```css
  @page { size: A4 landscape; margin: 0.6cm; }
  @media print {
    * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
    nav.lotr-nav, .sheet-toolbar, .above-sheet-bar { display: none !important; }
    body {
      background: white !important;
      font-size: 10px;
      padding: 0;
      min-height: unset;
    }
    main { padding: 0; }
    .sheet-wrap {
      max-width: 100%;
      width: 100%;
      box-shadow: none;
      border: 2px solid #8b6914;
      background-color: #f5ead6 !important;
      overflow: visible !important;
      zoom: 0.92;
    }
    .sheet-header {
      background-color: #ede0c0 !important;
      padding: 0.4rem 0.75rem;
    }
    .char-name-display { font-size: 1.1rem; }
    .tab-panel { display: none !important; }
    .tab-panel.active { display: block !important; padding: 0.6rem 0.75rem; break-inside: avoid; }
    .ability-col { gap: 0.35rem; }
    .ability-block { padding: 0.25rem 0.3rem; }
    .ability-score { font-size: 1.1rem; }
    .sheet-body { gap: 0.5rem; }
    .combat-top { gap: 0.35rem; }
    .check-item { line-height: 1.2; padding: 0.05rem 0; }
    .check-list { padding: 0.4rem 0.5rem; }
    .skills-col { gap: 0.4rem; }
    .combat-col { gap: 0.4rem; }
    .traits-col { gap: 0.35rem; }
    .trait-box { padding: 0.3rem 0.4rem; }
  }
  ```

- [ ] **Step 4: Save the file**

---

### Task 4: Manual verification and commit

**Files:**
- No changes — verification and commit only

- [ ] **Step 1: Open `frontend/character-sheet.html` directly in Chrome**

  The page will load with the DEV_PREVIEW data (Aragorn sheet). The screen layout should look identical to before — all changes are print-only.

- [ ] **Step 2: Print to PDF in Chrome**

  Press `Cmd+P` (Mac) or `Ctrl+P` (Windows) → "Save as PDF". Confirm:
  - Page count: **1**
  - All sections visible: character name, class/race, ability scores, skills, saving throws, combat stats, traits
  - No content clipped at the bottom

- [ ] **Step 3: Print to PDF in Firefox**

  Same as Step 2. Confirm page count is **1** and all sections are visible.

- [ ] **Step 4: Print to PDF in Safari (Mac only)**

  Same as Step 2. Confirm page count is **1** and all sections are visible.

- [ ] **Step 5: Verify screen display is unchanged**

  Switch between the Main, Background, and Spellcasting tabs in the browser. Confirm the layout looks the same as before the changes.

- [ ] **Step 6: Commit**

  ```bash
  git add frontend/character-sheet.html
  git commit -m "fix(SCRUM-57): fix print CSS dead rules and add zoom safety to prevent second page"
  ```

---

## Manual Verification Checklist (attach to PR)

For the PR description or ticket attachment, document:

| Browser | Page count | All sections visible | Notes |
|---|---|---|---|
| Chrome (version) | 1 | ✓ / ✗ | |
| Firefox (version) | 1 | ✓ / ✗ | |
| Safari (version) | 1 | ✓ / ✗ | |

Test with:
- Default DEV_PREVIEW (Aragorn — medium-length content)
- A premade with a long name and full abilities text (e.g. "Gandalf the Grey")
- A premade with minimal content