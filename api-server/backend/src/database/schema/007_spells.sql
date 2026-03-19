CREATE TABLE spells (
    spell_id SERIAL PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    spell_level INT NOT NULL,
    school spell_school NOT NULL,
    description TEXT,
    source_type source_type NOT NULL,
    source_name VARCHAR(100)
);
