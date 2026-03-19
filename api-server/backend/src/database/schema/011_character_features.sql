CREATE TABLE IF NOT EXISTS character_features (
    character_feature_id SERIAL PRIMARY KEY,
    character_id INTEGER NOT NULL REFERENCES characters(character_id),
    feature_id INTEGER NOT NULL REFERENCES features(feature_id)
);
