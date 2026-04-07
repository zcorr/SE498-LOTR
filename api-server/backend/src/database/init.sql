\set ON_ERROR_STOP on
SET client_min_messages TO warning;

-- Initialize database schema (Stats / premades JSON: charhealth for character health — see SPEC.md)
-- Run migrations in order

\i schema/001_classes.sql
\i schema/002_races.sql
\i schema/003_stats.sql
\i schema/004_abilities.sql
\i schema/005_premades.sql
