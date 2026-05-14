# SE498-LOTR

[![CI](https://github.com/zcorr/SE498-LOTR/actions/workflows/ci.yml/badge.svg)](https://github.com/zcorr/SE498-LOTR/actions/workflows/ci.yml)

A Lord of the Rings themed character generator that turns Tolkien-inspired
heroes into playable D&D-style character sheets. Users can log in, browse
premade characters, roll a new class/race build, save sheets, and edit saved
character details.

## What Runs Here

| Layer | Path | Tech | Default URL |
| --- | --- | --- | --- |
| MVC web app | [web-server/](web-server/) | ASP.NET Core 10 MVC, Npgsql, JWT cookies | http://localhost:5292 |
| Static pages | [frontend/](frontend/) | HTML, Bootstrap, vanilla JS | served by the MVC app |
| LOTR API | [api-server/backend/src/LotrApi/](api-server/backend/src/LotrApi/) | ASP.NET Core 10 minimal API, Npgsql | http://localhost:5030 |
| API database | [api-server/backend/src/database/](api-server/backend/src/database/) | Postgres, schema + seed SQL | `lotr` |
| Web database | [web-server/database/schema/](web-server/database/schema/) | Postgres, users + saved sheets | `lotr_users` |
| Web tests | [web-server.Tests/](web-server.Tests/) | xUnit, Moq, WebApplicationFactory | no Docker needed |
| API tests | [api-server/backend/src/LotrApi.Tests/](api-server/backend/src/LotrApi.Tests/) | xUnit, Testcontainers | Docker required |

The normal Makefile workflow runs both LOTR databases in one local Postgres
container named `lotr-pg`. The Docker Compose workflow uses separate Postgres
containers for the API and web databases.

Sponsored banner ads are proxied by the MVC app from the Jurassic movie service.
The Jurassic project is vendored as a Git submodule at
[external/Project1-jurassic-park](external/Project1-jurassic-park). `make`
initializes it automatically on a fresh checkout when the full stack is used.

## Architecture

```text
Browser
  |
  | http://localhost:5292
  v
MVC web app
  - serves frontend/*.html
  - authenticates users
  - stores JWT in AuthToken cookie
  - saves character sheets in lotr_users
  |
  | Bearer JWT to http://localhost:5030
  v
LOTR API
  - classes, races, stats, abilities
  - premade characters
  - generated sheets
  |
  v
Postgres lotr

MVC web app also calls Jurassic API for banner ads:
http://localhost:5081 by default.
```

## Prerequisites

Install:

1. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Docker Desktop or Docker Engine
3. Git
4. `make`

Optional:

- `psql`, if you want to inspect databases manually.

Default ports used by local development:

| Port | Service |
| --- | --- |
| `5292` | LOTR MVC web app |
| `5030` | LOTR API |
| `5432` | LOTR Postgres container |
| `5080` | Jurassic web app |
| `5081` | Jurassic movie API |
| `55433` | Jurassic Postgres host port |

## Quick Start

Start the full local demo stack:

```bash
make
```

This runs:

1. Jurassic web/API/Postgres stack.
2. LOTR Postgres container `lotr-pg`.
3. `lotr` and `lotr_users` database creation.
4. Web database schema migrations.
5. LOTR API on `http://localhost:5030`.
6. MVC web app on `http://localhost:5292`.

Open:

- App: http://localhost:5292/Auth/Login
- API Swagger UI: http://localhost:5030/swagger
- API health: http://localhost:5030/health

Default login:

| Username | Password |
| --- | --- |
| `admin` | `password` |

Press `Ctrl-C` in the terminal running `make` to stop the API and web app. The
Postgres containers stay running for faster restarts unless you run `make down`.

## LOTR-Only Startup

Use this when another team only needs the LOTR app/API/database, or when
Jurassic is not needed:

```bash
make lotr
```

That starts only:

- LOTR Postgres
- LOTR API
- LOTR MVC web app

It does not start Jurassic. Banner ads will be skipped unless a Jurassic API is
already running at the configured `JurassicApi__BaseUrl`.

## Docker Compose Startup

The MVC web app can also build and run without a local .NET SDK:

```bash
docker build -f web-server/Dockerfile -t lotr-web .
```

Run the containerized LOTR stack:

```bash
docker compose -f compose.yaml up --build
```

This starts:

- `lotr-web` on `http://localhost:5292`
- `lotr-api` on `http://localhost:5030`
- `lotr-db`
- `lotr-users-db`

The root Compose stack does not start Jurassic. If Jurassic is already running
on the host at `http://localhost:5081`, the MVC container reaches it through
`http://host.docker.internal:5081`. If not, the app still works and banner ads
are skipped.

Stop the Compose stack:

```bash
docker compose -f compose.yaml down
```

Stop and remove Compose database volumes:

```bash
docker compose -f compose.yaml down -v
```

Run the container smoke test:

```bash
./scripts/smoke-web-container.sh
```

The smoke test builds the Compose stack, waits for `/Auth/Login`, checks `/`
and `/css/site.css`, logs in with `admin` / `password`, and verifies `/premade`
and `/character/create`.

## Make Targets

```text
make            Start full stack: Jurassic + LOTR DB + API + MVC web app
make up         Same as make
make lotr       Start only LOTR DB + API + MVC web app
make dev        Run LOTR API + MVC web app, assuming DB is already running
make api        Run LOTR API only
make web        Run MVC web app only
make jurassic   Start Jurassic stack only
make db         Start LOTR Postgres, create DBs, apply web schema
make db-reset   Drop and recreate LOTR databases, then apply web schema
make db-psql    Open psql against lotr; override with DB=lotr_users
make test       Run web and API test suites
make down       Stop dev servers, LOTR Postgres, and Jurassic
make clean      Run make down, then dotnet clean for API and web app
make help       Print target help
```

Useful overrides:

```bash
make PG_PORT=5433 db
make DB=lotr_users db-psql
make JURASSIC_DIR=/path/to/Project1-jurassic-park jurassic
make JURASSIC_MOVIE_PORT=5091 jurassic
```

## Configuration

Default development settings:

| Setting | Default |
| --- | --- |
| LOTR API URL used by MVC | `http://localhost:5030` |
| Jurassic API URL used by MVC | `http://localhost:5081` |
| LOTR Postgres user/password | `postgres` / `postgres` |
| LOTR API database | `lotr` |
| LOTR web database | `lotr_users` |
| JWT dev secret | `Cool_Mega_Secret_Key_For_JWT_Token_Generation` |

Environment variable overrides:

| Variable | Used by | Purpose |
| --- | --- | --- |
| `LotrApi__BaseUrl` | MVC web app | Base URL for the LOTR API |
| `JurassicApi__BaseUrl` | MVC web app | Base URL for Jurassic banner ads |
| `Jwt__Secret` | MVC web app and LOTR API | Shared JWT signing secret |
| `ConnectionStrings__UsersConnection` | MVC web app | Web/users database |
| `ConnectionStrings__DefaultConnection` | LOTR API | API game-data database |

For Docker Compose, service-to-service URLs are different inside the Docker
network. The root [compose.yaml](compose.yaml) sets these values automatically.

## API Authentication

The LOTR API allows unauthenticated `GET /health`. Game-data endpoints require:

```text
Authorization: Bearer <jwt>
```

The MVC web app handles this for browser users by storing the JWT in the
`AuthToken` cookie and forwarding it to the API.

## LOTR API Endpoints

| Method | Path | Description |
| --- | --- | --- |
| GET | `/health` | API process liveness |
| GET | `/classes` | All classes |
| GET | `/class/{id}` | One class with `name`, `desc`, `racialids` |
| GET | `/class/{id}/abilities` | Abilities for one class |
| GET | `/race` | All races |
| GET | `/stats` | All stat definitions and base values |
| GET | `/stats/{name}` | Single stat by case-insensitive name |
| GET | `/charhealth` | Character health stat |
| GET | `/strength` | Strength stat |
| GET | `/abilities?class_id=N` | Abilities, optionally filtered by class |
| GET | `/premades` | Paginated/filterable premade list |
| GET | `/names` | Premade character names |
| POST | `/generate` | Generate a character from `class_id` and `race_id` |

`POST /generate` body:

```json
{
  "class_id": 1,
  "race_id": 1
}
```

The API schema lives under
[api-server/backend/src/database/schema/](api-server/backend/src/database/schema/).
`DatabaseBootstrap` applies it when the API starts. Seed data is in
[006_seed.sql](api-server/backend/src/database/schema/006_seed.sql).

## MVC Web Routes

| Path | Purpose |
| --- | --- |
| `/` | Home page |
| `/Auth/Login` | Login page |
| `/Auth/Register` | Registration page |
| `/premade` | Premade character browser |
| `/character/create` | Character generator page |
| `/character/sheet` | Generated or saved character sheet page |
| `/my-characters` | Saved character list |

## MVC JSON Routes

| Path | Purpose |
| --- | --- |
| `POST /api/auth/login` | Log in and set `AuthToken` cookie |
| `POST /api/auth/logout` | Clear `AuthToken` cookie |
| `POST /api/auth/register` | Register a user |
| `GET /api/banner-ads/movies` | Proxy Jurassic movie posters |
| `GET /api/premade/list` | List premades through the API |
| `GET /api/premade/names` | Search premade names through the API |
| `POST /api/premade/select/{id}` | Select a premade |
| `GET /api/character/races` | Proxy races |
| `GET /api/character/classes` | Proxy classes |
| `GET /api/character/abilities` | Proxy abilities |
| `GET /api/character/stats` | Proxy stats |
| `POST /api/character/generate` | Generate a character |
| `GET /api/gamedata/*` | Legacy/mirror game-data proxy endpoints |
| `GET /api/character/sheets` | List saved sheets for current user |
| `POST /api/character/sheets` | Save a sheet |
| `GET /api/character/sheets/{id}` | Get one saved sheet |
| `PUT /api/character/sheets/{id}` | Update one saved sheet |
| `DELETE /api/character/sheets/{id}` | Delete one saved sheet |

## Character Sheet Editing

Saved character sheets are editable after creation. The sheet page supports
editing:

- background, player name, and alignment
- personality traits, ideals, bonds, and flaws
- equipment
- user-authored features and traits
- attacks and spellcasting rows

Saving an existing sheet calls `PUT /api/character/sheets/{id}`. Saving a
premade copy calls `POST /api/character/sheets`.

Web schema migrations are applied by `make db`:

| Migration | Change |
| --- | --- |
| `001_users.sql` | Users table |
| `002_character_sheets.sql` | Saved character sheets |
| `003_add_sheet_text_fields.sql` | Text fields for editable sheet details |
| `004_add_sheet_equipment_features.sql` | Equipment and feature text |
| `005_add_sheet_attacks.sql` | JSONB attacks data |

## Tests

Run all tests:

```bash
make test
```

Equivalent commands:

```bash
dotnet test web-server.Tests/web-server.Tests.csproj
dotnet test api-server/backend/src/LotrApi.Tests/LotrApi.Tests.csproj
```

Notes:

- Web tests use mocked services and do not need Docker.
- API tests use Testcontainers and require Docker.
- CI builds and tests [api-server/backend/LotrApi.slnx](api-server/backend/LotrApi.slnx)
  against a GitHub Actions Postgres service container.

## Project Layout

```text
.
|-- Makefile
|-- compose.yaml
|-- scripts/
|   `-- smoke-web-container.sh
|-- frontend/
|   |-- login.html
|   |-- register.html
|   |-- premades.html
|   |-- create.html
|   |-- character-sheet.html
|   `-- my-characters.html
|-- web-server/
|   |-- Controllers/
|   |-- Services/
|   |-- Views/
|   |-- wwwroot/
|   |-- database/schema/
|   |-- Dockerfile
|   `-- web-server.csproj
|-- web-server.Tests/
|-- api-server/
|   |-- backend/
|   |   |-- LotrApi.slnx
|   |   `-- src/
|   |       |-- LotrApi/
|   |       |-- LotrApi.Tests/
|   |       `-- database/
|   `-- docs/
|-- external/
|   `-- Project1-jurassic-park/
|-- SPEC.md
`-- README.md
```

## Troubleshooting

**Port already in use**

Run:

```bash
make down
```

If port `5432` is still occupied by another local Postgres, either stop that
process or run the LOTR DB on another host port:

```bash
make PG_PORT=5433 db
```

If you change `PG_PORT`, also update the local app connection strings or use
matching `ConnectionStrings__*` environment variables.

**Jurassic banner ads do not show**

The LOTR app still works without ads. To enable ads, start Jurassic:

```bash
make jurassic
```

The MVC app expects the Jurassic API at `http://localhost:5081` by default.
Override it with:

```bash
JurassicApi__BaseUrl=http://localhost:5081 make web
```

**Login fails with invalid credentials**

The seeded admin user is created when the MVC app starts. If you reset the web
database, restart the MVC app and try again:

```text
admin / password
```

**API returns 401**

The MVC app and API must share the same `Jwt__Secret`.

**Testcontainers errors**

Start Docker Desktop or Docker Engine, then rerun:

```bash
make test
```

**Clean reset**

```bash
make clean
make
```
