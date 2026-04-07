-- Idempotent seed: premades Gandalf the Grey, Frodo, Gollum + reference rows (SCRUM-14).
-- premades.stats uses key "charhealth" per SPEC / 005_premades.sql.

INSERT INTO races (id, name, modifiers)
VALUES
    (1, 'Hobbit', 'Nimble; hardy'),
    (2, 'Human', 'Adaptable'),
    (3, 'Elf', 'Keen senses'),
    (4, 'Orc', 'Brutish strength')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    modifiers = EXCLUDED.modifiers;

INSERT INTO classes (id, name, description, racial_ids)
VALUES
    (1, 'Warrior', 'A fierce fighter trained for front-line combat.', ARRAY[1, 2, 3, 4]::integer[]),
    (2, 'Istari', 'A member of the order of wizards.', ARRAY[2, 3]::integer[]),
    (3, 'Burglar', 'Stealth and cleverness for perilous journeys.', ARRAY[1]::integer[]),
    (4, 'Creature', 'Twisted and sustained by unnatural will.', ARRAY[1]::integer[])
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    racial_ids = EXCLUDED.racial_ids;

INSERT INTO stats (name, base_value)
VALUES
    ('charhealth', 20),
    ('strength', 10)
ON CONFLICT (name) DO UPDATE SET base_value = EXCLUDED.base_value;

INSERT INTO abilities (name, description, class_id)
SELECT v.name, v.description, v.class_id
FROM (VALUES
    ('Heavy Strike', 'Deal extra damage in melee.', 1),
    ('Shield Wall', 'Brace for incoming blows.', 1),
    ('You Shall Not Pass', 'Stand firm against the foe.', 2),
    ('Fire of Anor', 'Unveil blinding light.', 2),
    ('Quiet Step', 'Move unseen when it matters.', 3),
    ('Lucky Escape', 'Slip away from danger.', 3),
    ('Cling to the Precious', 'Endure through obsession.', 4),
    ('Crawl and Bite', 'Fight dirty when cornered.', 4)
) AS v(name, description, class_id)
WHERE NOT EXISTS (
    SELECT 1 FROM abilities a WHERE a.name = v.name AND a.class_id = v.class_id
);

INSERT INTO premades (name, class_id, race_id, stats)
VALUES
    (
        'Gandalf the Grey',
        2,
        2,
        '{"charhealth": 120, "strength": 12}'::jsonb
    ),
    (
        'Frodo',
        3,
        1,
        '{"charhealth": 45, "strength": 6}'::jsonb
    ),
    (
        'Gollum',
        4,
        1,
        '{"charhealth": 35, "strength": 8}'::jsonb
    )
ON CONFLICT (name) DO UPDATE SET
    class_id = EXCLUDED.class_id,
    race_id = EXCLUDED.race_id,
    stats = EXCLUDED.stats;

SELECT setval(pg_get_serial_sequence('races', 'id'), COALESCE((SELECT MAX(id) FROM races), 1));
SELECT setval(pg_get_serial_sequence('classes', 'id'), COALESCE((SELECT MAX(id) FROM classes), 1));
SELECT setval(pg_get_serial_sequence('stats', 'id'), COALESCE((SELECT MAX(id) FROM stats), 1));
SELECT setval(pg_get_serial_sequence('abilities', 'id'), COALESCE((SELECT MAX(id) FROM abilities), 1));
SELECT setval(pg_get_serial_sequence('premades', 'id'), COALESCE((SELECT MAX(id) FROM premades), 1));
