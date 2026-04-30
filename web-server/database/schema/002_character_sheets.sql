CREATE TABLE IF NOT EXISTS character_sheets (
                                                id SERIAL PRIMARY KEY,
                                                user_id INTEGER NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    class_name VARCHAR(255) NOT NULL,
    race_name VARCHAR(255) NOT NULL,
    class_description TEXT,
    race_modifiers TEXT,
    stats JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
    );

CREATE INDEX IF NOT EXISTS idx_character_sheets_user_id ON character_sheets (user_id);