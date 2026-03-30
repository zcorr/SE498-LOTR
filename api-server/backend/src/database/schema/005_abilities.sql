CREATE TABLE IF NOT EXISTS abilities (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    class_id INTEGER NOT NULL REFERENCES classes (id)
);
