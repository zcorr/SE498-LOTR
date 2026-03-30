CREATE TABLE IF NOT EXISTS premades (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    class_id INTEGER NOT NULL REFERENCES classes (id),
    race_id INTEGER NOT NULL REFERENCES races (id),
    stats JSONB NOT NULL DEFAULT '{}'::jsonb
);
