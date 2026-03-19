CREATE TABLE features (
    feature_id SERIAL PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    feature_type feature_type NOT NULL,
    description TEXT,
    source_type source_type NOT NULL,
    source_name VARCHAR(100)
);
