CREATE TABLE characters (
    character_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    level INT NOT NULL DEFAULT 1,
    subrace_name VARCHAR(100),
    subclass_name VARCHAR(100),
    race_id INTEGER REFERENCES races(race_id),
    class_id INTEGER REFERENCES classes(class_id),
    background_id INTEGER REFERENCES backgrounds(background_id)
);
