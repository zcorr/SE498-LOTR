\set ON_ERROR_STOP on
SET client_min_messages TO warning;

-- Drop application tables in reverse dependency order.
DROP TABLE IF EXISTS premades CASCADE;
DROP TABLE IF EXISTS abilities CASCADE;
DROP TABLE IF EXISTS classes CASCADE;
DROP TABLE IF EXISTS race CASCADE;
DROP TABLE IF EXISTS stats CASCADE;
DROP TABLE IF EXISTS species CASCADE;
