-- Idempotent LOTR library seed (SCRUM-53).
-- premades.stats uses key "charhealth" per SPEC / 005_premades.sql.
-- Lore mapping used here:
--   Frodo, Sam -> Hobbit Burglars for stealth and resilience.
--   Aragorn, Boromir, Legolas, Gimli -> front-line Warriors matching martial roles.
--   Gandalf, Galadriel, Saruman -> Istari-style casters to capture wisdom / magic.
--   Gollum -> Creature to reflect his corrupted survivalist profile.

INSERT INTO races (id, name, modifiers)
VALUES
    (1, 'Hobbit', 'Nimble; hardy'),
    (2, 'Human', 'Adaptable'),
    (3, 'Elf', 'Keen senses'),
    (4, 'Dwarf', 'Stout; unyielding')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    modifiers = EXCLUDED.modifiers;

INSERT INTO classes (id, name, description, racial_ids)
VALUES
    (1, 'Warrior', 'A fierce fighter trained for front-line combat.', ARRAY[1, 2, 3, 4]::integer[]),
    (2, 'Istari', 'A member of the order of wizards.', ARRAY[2, 3]::integer[]),
    (3, 'Burglar', 'Stealth and cleverness for perilous journeys.', ARRAY[1, 2, 3]::integer[]),
    (4, 'Creature', 'Twisted and sustained by unnatural will.', ARRAY[1]::integer[])
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    racial_ids = EXCLUDED.racial_ids;

INSERT INTO stats (name, base_value)
VALUES
    ('charhealth', 20),
    ('strength', 10),
    ('dexterity', 10),
    ('constitution', 10),
    ('intelligence', 10),
    ('wisdom', 10),
    ('charisma', 10)
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
        '{"charhealth": 120, "strength": 12, "dexterity": 13, "constitution": 16, "intelligence": 18, "wisdom": 19, "charisma": 18}'::jsonb
    ),
    (
        'Frodo',
        3,
        1,
        '{"charhealth": 55, "strength": 8, "dexterity": 14, "constitution": 12, "intelligence": 11, "wisdom": 13, "charisma": 14}'::jsonb
    ),
    (
        'Samwise Gamgee',
        3,
        1,
        '{"charhealth": 60, "strength": 10, "dexterity": 12, "constitution": 14, "intelligence": 10, "wisdom": 14, "charisma": 13}'::jsonb
    ),
    (
        'Aragorn',
        1,
        2,
        '{"charhealth": 95, "strength": 17, "dexterity": 14, "constitution": 16, "intelligence": 13, "wisdom": 15, "charisma": 16}'::jsonb
    ),
    (
        'Legolas',
        1,
        3,
        '{"charhealth": 82, "strength": 13, "dexterity": 18, "constitution": 12, "intelligence": 13, "wisdom": 14, "charisma": 15}'::jsonb
    ),
    (
        'Gimli',
        1,
        4,
        '{"charhealth": 100, "strength": 18, "dexterity": 10, "constitution": 17, "intelligence": 11, "wisdom": 12, "charisma": 10}'::jsonb
    ),
    (
        'Boromir',
        1,
        2,
        '{"charhealth": 92, "strength": 17, "dexterity": 13, "constitution": 16, "intelligence": 12, "wisdom": 11, "charisma": 15}'::jsonb
    ),
    (
        'Gollum',
        4,
        1,
        '{"charhealth": 45, "strength": 8, "dexterity": 17, "constitution": 10, "intelligence": 12, "wisdom": 9, "charisma": 6}'::jsonb
    ),
    (
        'Galadriel',
        2,
        3,
        '{"charhealth": 110, "strength": 10, "dexterity": 14, "constitution": 14, "intelligence": 19, "wisdom": 20, "charisma": 18}'::jsonb
    ),
    (
        'Saruman the White',
        2,
        2,
        '{"charhealth": 108, "strength": 10, "dexterity": 12, "constitution": 14, "intelligence": 19, "wisdom": 18, "charisma": 17}'::jsonb
    ),
    (
        'Meriadoc Brandybuck',
        3,
        1,
        '{"charhealth": 52, "strength": 8, "dexterity": 13, "constitution": 12, "intelligence": 11, "wisdom": 12, "charisma": 13}'::jsonb
    ),
    (
        'Peregrin Took',
        3,
        1,
        '{"charhealth": 50, "strength": 8, "dexterity": 13, "constitution": 11, "intelligence": 10, "wisdom": 11, "charisma": 14}'::jsonb
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
