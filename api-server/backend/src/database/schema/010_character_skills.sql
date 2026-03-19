CREATE TABLE character_skills (
    character_skill_id SERIAL PRIMARY KEY,
    character_id INTEGER NOT NULL REFERENCES characters(character_id),
    skill_id INTEGER NOT NULL REFERENCES skills(skill_id),
    is_proficient BOOLEAN NOT NULL DEFAULT FALSE,
    UNIQUE (character_id, skill_id)
);
