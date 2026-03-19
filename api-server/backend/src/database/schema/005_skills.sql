CREATE TABLE skills (
    skill_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    ability_type_id INTEGER NOT NULL REFERENCES ability_types(ability_type_id)
);
