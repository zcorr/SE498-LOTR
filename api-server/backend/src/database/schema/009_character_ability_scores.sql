CREATE TABLE IF NOT EXISTS character_ability_scores (
    character_ability_score_id SERIAL PRIMARY KEY,
    character_id INTEGER NOT NULL REFERENCES characters(character_id),
    ability_type_id INTEGER NOT NULL REFERENCES ability_types(ability_type_id),
    score INT NOT NULL,
    UNIQUE (character_id, ability_type_id)
);
