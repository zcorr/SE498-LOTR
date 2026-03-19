-- Enum types used across the schema

DO $$
BEGIN
    CREATE TYPE size_category AS ENUM (
        'Tiny',
        'Small',
        'Medium',
        'Large',
        'Huge',
        'Gargantuan'
    );
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

DO $$
BEGIN
    CREATE TYPE spell_school AS ENUM (
        'Abjuration',
        'Conjuration',
        'Divination',
        'Enchantment',
        'Evocation',
        'Illusion',
        'Necromancy',
        'Transmutation'
    );
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

DO $$
BEGIN
    CREATE TYPE feature_type AS ENUM (
        'Class Feature',
        'Racial Trait',
        'Background Feature',
        'Feat'
    );
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

DO $$
BEGIN
    CREATE TYPE source_type AS ENUM (
        'Class',
        'Race',
        'Background',
        'Feat'
    );
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;
