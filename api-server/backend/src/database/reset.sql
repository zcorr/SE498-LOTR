\set ON_ERROR_STOP on
SET client_min_messages TO warning;

-- Drop application tables in reverse dependency order.
DROP TABLE IF EXISTS character_spells CASCADE;
DROP TABLE IF EXISTS character_features CASCADE;
DROP TABLE IF EXISTS character_skills CASCADE;
DROP TABLE IF EXISTS character_ability_scores CASCADE;
DROP TABLE IF EXISTS characters CASCADE;
DROP TABLE IF EXISTS spells CASCADE;
DROP TABLE IF EXISTS features CASCADE;
DROP TABLE IF EXISTS skills CASCADE;
DROP TABLE IF EXISTS races CASCADE;
DROP TABLE IF EXISTS classes CASCADE;
DROP TABLE IF EXISTS backgrounds CASCADE;
DROP TABLE IF EXISTS ability_types CASCADE;

-- Drop enum types after dependent tables are gone.
DROP TYPE IF EXISTS source_type CASCADE;
DROP TYPE IF EXISTS feature_type CASCADE;
DROP TYPE IF EXISTS spell_school CASCADE;
DROP TYPE IF EXISTS size_category CASCADE;
