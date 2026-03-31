-- Character health for GET /charhealth: seed name = 'charhealth' (not 'health'; GET /health is server liveness).
CREATE TABLE IF NOT EXISTS stats (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    base_value INTEGER NOT NULL
);
