CREATE TABLE classes (
    class_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    description TEXT,
    hit_die INT NOT NULL,
    spellcasting_ability_id INTEGER REFERENCES ability_types(ability_type_id)
);
