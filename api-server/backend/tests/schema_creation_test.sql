\set ON_ERROR_STOP on
SET client_min_messages TO warning;

CREATE OR REPLACE FUNCTION pg_temp.assert_true(condition boolean, message text)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT condition THEN
        RAISE EXCEPTION '%', message;
    END IF;
END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.assert_table_exists(expected_table text)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM pg_temp.assert_true(
        to_regclass(format('public.%I', expected_table)) IS NOT NULL,
        format('Expected table public.%I to exist', expected_table)
    );
END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.assert_columns(expected_table text, expected_columns text[])
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    actual_columns text[];
BEGIN
    SELECT array_agg(column_name::text ORDER BY ordinal_position)
    INTO actual_columns
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = expected_table;

    PERFORM pg_temp.assert_true(
        actual_columns = expected_columns,
        format(
            'Expected columns for public.%I to be %s, got %s',
            expected_table,
            expected_columns,
            COALESCE(actual_columns::text, 'NULL')
        )
    );
END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.assert_primary_key(expected_table text, expected_columns text[])
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    actual_columns text[];
BEGIN
    SELECT array_agg(kcu.column_name::text ORDER BY kcu.ordinal_position)
    INTO actual_columns
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
      ON tc.constraint_name = kcu.constraint_name
     AND tc.table_schema = kcu.table_schema
     AND tc.table_name = kcu.table_name
    WHERE tc.table_schema = 'public'
      AND tc.table_name = expected_table
      AND tc.constraint_type = 'PRIMARY KEY';

    PERFORM pg_temp.assert_true(
        actual_columns = expected_columns,
        format(
            'Expected primary key for public.%I to be %s, got %s',
            expected_table,
            expected_columns,
            COALESCE(actual_columns::text, 'NULL')
        )
    );
END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.assert_unique_key(expected_table text, expected_columns text[])
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM pg_temp.assert_true(
        EXISTS (
            SELECT 1
            FROM (
                SELECT tc.constraint_name,
                       array_agg(kcu.column_name::text ORDER BY kcu.ordinal_position) AS columns
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                  ON tc.constraint_name = kcu.constraint_name
                 AND tc.table_schema = kcu.table_schema
                 AND tc.table_name = kcu.table_name
                WHERE tc.table_schema = 'public'
                  AND tc.table_name = expected_table
                  AND tc.constraint_type = 'UNIQUE'
                GROUP BY tc.constraint_name
            ) unique_constraints
            WHERE unique_constraints.columns = expected_columns
        ),
        format(
            'Expected unique constraint on public.%I for columns %s',
            expected_table,
            expected_columns
        )
    );
END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.assert_foreign_key(
    expected_table text,
    expected_column text,
    referenced_table text,
    referenced_column text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM pg_temp.assert_true(
        EXISTS (
            SELECT 1
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name
             AND tc.table_schema = kcu.table_schema
             AND tc.table_name = kcu.table_name
            JOIN information_schema.constraint_column_usage ccu
              ON tc.constraint_name = ccu.constraint_name
             AND tc.table_schema = ccu.table_schema
            WHERE tc.table_schema = 'public'
              AND tc.table_name = expected_table
              AND tc.constraint_type = 'FOREIGN KEY'
              AND kcu.column_name = expected_column
              AND ccu.table_name = referenced_table
              AND ccu.column_name = referenced_column
        ),
        format(
            'Expected foreign key public.%I(%I) -> public.%I(%I)',
            expected_table,
            expected_column,
            referenced_table,
            referenced_column
        )
    );
END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.assert_udt(expected_table text, expected_column text, expected_udt text)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    actual_udt text;
BEGIN
    SELECT udt_name
    INTO actual_udt
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = expected_table
      AND column_name = expected_column;

    PERFORM pg_temp.assert_true(
        actual_udt = expected_udt,
        format(
            'Expected public.%I.%I to use type %s, got %s',
            expected_table,
            expected_column,
            expected_udt,
            COALESCE(actual_udt, 'NULL')
        )
    );
END;
$$;

SELECT pg_temp.assert_table_exists('classes');
SELECT pg_temp.assert_columns('classes', ARRAY['id', 'name', 'description']);
SELECT pg_temp.assert_primary_key('classes', ARRAY['id']);
SELECT pg_temp.assert_unique_key('classes', ARRAY['name']);

SELECT pg_temp.assert_table_exists('race');
SELECT pg_temp.assert_columns('race', ARRAY['id', 'name', 'modifiers']);
SELECT pg_temp.assert_primary_key('race', ARRAY['id']);
SELECT pg_temp.assert_unique_key('race', ARRAY['name']);

SELECT pg_temp.assert_table_exists('stats');
SELECT pg_temp.assert_columns('stats', ARRAY['id', 'name', 'base_value']);
SELECT pg_temp.assert_primary_key('stats', ARRAY['id']);
SELECT pg_temp.assert_unique_key('stats', ARRAY['name']);

SELECT pg_temp.assert_table_exists('species');
SELECT pg_temp.assert_columns('species', ARRAY['id', 'name', 'traits']);
SELECT pg_temp.assert_primary_key('species', ARRAY['id']);
SELECT pg_temp.assert_unique_key('species', ARRAY['name']);

SELECT pg_temp.assert_table_exists('abilities');
SELECT pg_temp.assert_columns('abilities', ARRAY['id', 'name', 'description', 'class_id']);
SELECT pg_temp.assert_primary_key('abilities', ARRAY['id']);
SELECT pg_temp.assert_foreign_key('abilities', 'class_id', 'classes', 'id');

SELECT pg_temp.assert_table_exists('premades');
SELECT pg_temp.assert_columns('premades', ARRAY['id', 'name', 'class_id', 'race_id', 'stats']);
SELECT pg_temp.assert_primary_key('premades', ARRAY['id']);
SELECT pg_temp.assert_unique_key('premades', ARRAY['name']);
SELECT pg_temp.assert_foreign_key('premades', 'class_id', 'classes', 'id');
SELECT pg_temp.assert_foreign_key('premades', 'race_id', 'race', 'id');
SELECT pg_temp.assert_udt('premades', 'stats', 'jsonb');

SELECT 'schema creation tests passed' AS result;
