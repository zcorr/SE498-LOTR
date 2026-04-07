# SE498-LOTR

[![CI](https://github.com/zcorr/SE498-LOTR/actions/workflows/ci.yml/badge.svg)](https://github.com/zcorr/SE498-LOTR/actions/workflows/ci.yml)

SE498-LOTR is a Lord of the Rings themed character generator that translates iconic Tolkien characters into playable Dungeons & Dragons character sheets. The goal is to make it easy for fans to browse familiar heroes, generate lore-informed builds, customize them for a campaign, and save or share the final result.

## Overview

This project focuses on turning Middle-earth characters into usable tabletop RPG sheets without forcing users to build everything by hand. A generated sheet should feel recognizable to the source material while still fitting into a D&D ruleset.

Users can explore a library of characters, search for specific names, generate a sheet based on lore, and then adjust the result to better match their table. The experience is intended to work for both D&D players and Lord of the Rings fans who may need extra guidance on what each field means.

## Core Features

### Character Discovery

- Browse a roster of Lord of the Rings characters.
- Search by name to quickly locate a specific character.
- Select a character and generate a D&D sheet based on their lore, abilities, and role in the story.

### Sheet Generation

- Produce class recommendations and core ability scores:
  - Strength
  - Dexterity
  - Constitution
  - Intelligence
  - Wisdom
  - Charisma
- Include a short lore summary or backstory for roleplay context.
- Populate relevant skills, proficiencies, traits, and other sheet details automatically.
- Generate spells or special abilities when a character’s source material supports them.
- Display portrait art or character imagery to make the sheet more immersive.

### Customization

- Allow manual stat adjustments after generation.
- Support multiple D&D editions so groups can use the ruleset they prefer.
- Let users add custom notes, equipment, or campaign-specific changes.

### Saving and Sharing

- Create accounts to save generated character sheets.
- Export sheets as PDFs for printing or offline use.
- Share builds through unique links with party members or a DM.

### Accessibility for New Players

- Provide tooltips or plain-language explanations for D&D fields.
- Help Lord of the Rings fans understand how lore has been translated into tabletop mechanics.

## Example User Flow

1. Open the app and browse or search for a Lord of the Rings character.
2. Choose a preferred D&D edition.
3. Generate a character sheet with stats, traits, lore, and abilities mapped from the source material.
4. Adjust numbers, notes, or equipment as needed for a campaign.
5. Save the sheet to an account, export it as a PDF, or share it with others.

## Design Goals

- Keep generated characters faithful to Tolkien-inspired lore.
- Reduce manual character sheet setup.
- Make the system approachable for users who know LotR better than D&D.
- Support both quick generation and deeper customization.

## Future Considerations

- Expanded character library across books and films.
- More advanced balancing controls for different campaign styles.
- Additional export and sharing formats.
- Stronger edition-specific rule mapping.

## Project Vision

SE498-LOTR aims to sit between fandom and tabletop play: a tool that lets users bring recognizable Middle-earth characters to the table with less setup, more flavor, and enough flexibility to fit their group’s campaign.

## Running Tests Locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL 14+ running locally (or via Docker)

### Set up the test database

```bash
# Using the Makefile (requires psql on PATH and a local Postgres instance)
make test-schema

# Or manually:
createdb lotr_test
cd api-server/backend/src/database
psql -d lotr_test -f reset.sql
psql -d lotr_test -f init.sql
```

### Run the C# unit/integration tests

```bash
# Set the connection string for the test database
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=lotr_test;Username=$(whoami);Password="

dotnet test api-server/backend/LotrApi.slnx --verbosity normal
```

The CI workflow (`.github/workflows/ci.yml`) runs the same steps automatically on every push and pull request.
