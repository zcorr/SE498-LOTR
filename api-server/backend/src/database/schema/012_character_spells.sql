CREATE TABLE character_spells (
    character_spell_id SERIAL PRIMARY KEY,
    character_id INTEGER NOT NULL REFERENCES characters(character_id),
    spell_id INTEGER NOT NULL REFERENCES spells(spell_id),
    is_known BOOLEAN NOT NULL DEFAULT FALSE,
    is_prepared BOOLEAN NOT NULL DEFAULT FALSE,
    UNIQUE (character_id, spell_id)
);
