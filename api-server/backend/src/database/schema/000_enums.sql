-- Enum types used across the schema

CREATE TYPE size_category AS ENUM (
    'Tiny',
    'Small',
    'Medium',
    'Large',
    'Huge',
    'Gargantuan'
);

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

CREATE TYPE feature_type AS ENUM (
    'Class Feature',
    'Racial Trait',
    'Background Feature',
    'Feat'
);

CREATE TYPE source_type AS ENUM (
    'Class',
    'Race',
    'Background',
    'Feat'
);
