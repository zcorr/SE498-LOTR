-- 005_add_sheet_attacks.sql
ALTER TABLE character_sheets
  ADD COLUMN IF NOT EXISTS attacks JSONB;