CREATE TABLE IF NOT EXISTS races (
    race_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    description TEXT,
    movement_speed INT DEFAULT 30,
    size size_category DEFAULT 'Medium'
);
