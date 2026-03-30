\set ON_ERROR_STOP on
SET client_min_messages TO warning;

-- Full reset: drop and recreate public so legacy tables, types, and other
-- objects from older schemas cannot linger after make reset-db.
DROP SCHEMA IF EXISTS public CASCADE;
CREATE SCHEMA public;
GRANT ALL ON SCHEMA public TO PUBLIC;
