-- 003_add_sheet_text_fields.sql
ALTER TABLE character_sheets
  ADD COLUMN IF NOT EXISTS background        TEXT,
  ADD COLUMN IF NOT EXISTS player_name       TEXT,
  ADD COLUMN IF NOT EXISTS alignment         TEXT,
  ADD COLUMN IF NOT EXISTS personality_traits TEXT,
  ADD COLUMN IF NOT EXISTS ideals            TEXT,
  ADD COLUMN IF NOT EXISTS bonds             TEXT,
  ADD COLUMN IF NOT EXISTS flaws             TEXT;