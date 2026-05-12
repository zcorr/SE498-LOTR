-- 004_add_sheet_equipment_features.sql
ALTER TABLE character_sheets
  ADD COLUMN IF NOT EXISTS equipment      TEXT,
  ADD COLUMN IF NOT EXISTS features_traits TEXT;
