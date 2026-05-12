# SCRUM-57: PDF Character Sheet Second-Page Overflow Fix

**Date:** 2026-05-08
**Ticket:** SCRUM-57
**File affected:** `frontend/character-sheet.html`

## Problem

Generated character-sheet PDFs (produced via `window.print()` → Save as PDF) sometimes spill onto a second page. The bug is browser-dependent — Chrome, Firefox, and Safari render print layouts slightly differently, and the sheet sits right on the edge of the A4 landscape printable area.

## Root Causes Identified

1. **Dead CSS rules in `@media print`** — The print block references five class names that do not exist in the HTML. The intended spacing reductions never fire:

   | Rule in print CSS | Actual class name |
   |---|---|
   | `.stat-block` | `.ability-col` |
   | `.stat-box` | `.ability-block` |
   | `.stat-score` | `.ability-score` |
   | `.section-body` | `.sheet-body` |
   | `.combat-grid` | `.combat-top` |

2. **`overflow: hidden` not reset for print** — `.sheet-wrap` has `overflow: hidden` in screen styles and this is not overridden in `@media print`, which can cause the browser to mishandle content height.

3. **No `break-inside` control** — Nothing prevents the browser from splitting the sheet mid-element across pages.

4. **Tall skills column** — The skills list (18 items) + saving throws (6 items) = 24 stacked rows, which at print font size can exceed the A4 landscape printable height (~198mm) if spacing is not tightened.

## Design

### Scope

All changes are confined to the `@media print` block inside `frontend/character-sheet.html`. No JS changes, no new files, no screen-style changes, no page-size change (A4 landscape is kept).

### Fix A — Correct dead CSS rules and tighten print spacing

Replace all five dead rules with their correct class-name targets. Apply spacing tightening to the elements most likely to overflow:

- `.ability-block` — reduce padding
- `.ability-score` — reduce font size
- `.sheet-body` — reduce gap
- `.combat-top` — reduce gap
- `.check-item` — reduce line-height and vertical padding (24 rows, the tallest section)
- `.check-list` — reduce padding
- `.trait-box` — reduce padding
- `.skills-col`, `.combat-col`, `.traits-col` — reduce gap

Also add:
- `overflow: visible !important` on `.sheet-wrap` in print
- `break-inside: avoid` on `.tab-panel.active` to prevent mid-sheet splits

### Fix B — `zoom` safety net

Add `zoom: 0.92` to `.sheet-wrap` in the print block. This proportionally scales the entire sheet to 92% of its rendered size. Unlike `transform: scale()`, `zoom` participates in layout flow so browsers use the zoomed height when calculating page breaks. The 8% reduction recovers enough vertical space to absorb browser-to-browser rendering variance.

Browser support: Chrome/Safari (always), Firefox 126+ (now broadly deployed).

## Acceptance Criteria

- Standard generated and saved character sheets render as a single page in Chrome, Firefox, and Safari when using Print → Save as PDF.
- Long character names, ability lists, and trait text wrap or scale in a controlled way without pushing sections onto page 2.
- All required sections are preserved in the PDF: character identity, class, race, stats, abilities, and traits.
- Empty or short content does not create a blank trailing page.
- Screen display is unaffected (all changes are inside `@media print`).

## Out of Scope

- Server-side PDF generation
- Page size changes
- HTML structure changes
- Automated page-count regression test (manual verification checklist is sufficient per ticket)