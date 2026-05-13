# SE498-LOTR

[![CI](https://github.com/zcorr/SE498-LOTR/actions/workflows/ci.yml/badge.svg)](https://github.com/zcorr/SE498-LOTR/actions/workflows/ci.yml)

A Lord of the Rings themed character generator that turns iconic Tolkien
characters into playable D&D-style character sheets. Pick a premade hero
(Aragorn, Gandalf, Frodo…), or roll a fresh build from a class + race, then
save it to your account.

## Architecture

Three-tier, two databases, plus the Jurassic movie service used for sponsored
banner ads:

```
                      ┌────────────────────┐
  Browser  ───────►   │  Web Server (MVC)  │  :5292
                      │   web-server/      │
                      │   serves frontend/ │
                      │   issues JWT cookie│
                      └─────────┬──────────┘
                                │ Bearer JWT
                                ▼
                      ┌────────────────────┐
                      │  API Server        │  :5030
                      │   api-server/      │
                      │   game data + gen  │
                      └─────────┬──────────┘
                                │
                ┌───────────────┴───────────────┐
                ▼                               ▼
         Postgres "lotr"                 Postgres "lotr_users"
         (classes, races, stats,         (users, character_sheets)
          abilities, premades)
```

Both Postgres databases run in a single container (`lotr-pg`,
`postgres:16-alpine`) on port 5432.

Sponsored banner ads are loaded through the web server from the Jurassic movie
service at `http://localhost:5080`. The Jurassic project is included as a Git
submodule at [external/Project1-jurassic-park](external/Project1-jurassic-park),
and `make` initializes it automatically on a fresh checkout if needed. If you
want to use a separate local checkout, pass
`JURASSIC_DIR=/path/to/Project1-jurassic-park`.

| Layer            | Path                             | Tech                          | Default URL              |
| ---------------- | -------------------------------- | ----------------------------- | ------------------------ |
| Frontend (HTML)  | [frontend/](frontend/)           | HTML + Bootstrap + vanilla JS | served by web-server     |
| Web server (MVC) | [web-server/](web-server/)       | ASP.NET Core 10, Npgsql, JWT  | http://localhost:5292    |
| API server       | [api-server/](api-server/)       | ASP.NET Core 10 minimal API   | http://localhost:5030    |
| API tests        | [api-server/backend/src/LotrApi.Tests/](api-server/backend/src/LotrApi.Tests/) | xUnit + Testcontainers | (Docker required) |
| Web tests        | [web-server.Tests/](web-server.Tests/) | xUnit + Moq + WebApplicationFactory | (no Docker needed) |

## Prerequisites

You need four things on your machine:

1. **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** — verify with `dotnet --version` (should print `10.x.x`).
2. **Docker** (Desktop or Engine) — used to run Postgres and required by the API integration tests. Verify with `docker info`.
3. **Git** — needed to initialize the Jurassic submodule on first run.
4. **make** — preinstalled on macOS / most Linux distros. On Windows use WSL or install via [chocolatey](https://chocolatey.org/) (`choco install make`).

Optional: `psql` on your `PATH` if you want to poke at the databases outside of `make db-psql`.

Port 5432 (Postgres), 5030 (API), and 5292 (web) must be free.

## Quick start

From the repo root:

```bash
make
```

That single command:

1. Initializes the Jurassic submodule if needed.
2. Starts the Jurassic movie service stack (`:5080` API, `:5044` web, Postgres on host `:5433`).
3. Starts (or reuses) the `lotr-pg` Postgres container.
4. Creates the `lotr` and `lotr_users` databases if they don't exist.
5. Applies the web-server user/sheet schema (the API server applies its own schema on startup).
6. Boots the API server on `:5030` and the web server on `:5292` in parallel.

Then open:

- **App:** http://localhost:5292/Auth/Login
- **API Swagger UI:** http://localhost:5030/swagger
- **API health:** http://localhost:5030/health

Default seeded login:

| Username | Password |
| -------- | -------- |
| `admin`  | `password` |

You can also register a new account from the login page. Press **Ctrl-C** in the
terminal running `make` to stop both servers; the Postgres container keeps
running so the next `make` is fast.

If you only need the LOTR-owned app/API/database stack and do not want to start
Jurassic, run:

```bash
make lotr
```

## Docker quick start

The MVC web app can be built and run without a local .NET SDK by using Docker
from the repo root:

```bash
docker build -f web-server/Dockerfile -t lotr-web .
```

Run the full local stack with Docker Compose:

```bash
docker compose -f compose.yaml up --build
```

Then open:

- **App:** http://localhost:5292/Auth/Login
- **API health:** http://localhost:5030/health

Stop the stack with:

```bash
docker compose -f compose.yaml down
```

Remove database volumes for a clean reset:

```bash
docker compose -f compose.yaml down -v
```

Run the MVC container smoke test:

```bash
./scripts/smoke-web-container.sh
```

The smoke test builds the compose stack, waits for `/Auth/Login`, checks `/`
and `/css/site.css`, logs in with the seeded admin account, and confirms the
authenticated `/premade` and `/character/create` routes return successful
responses from inside the containerized MVC app.

The compose setup includes the MVC app, LOTR API, and two Postgres databases.
The Jurassic movie service is optional for this compose stack. If it is running
on the host at `http://localhost:5081`, the MVC container reaches it through
`http://host.docker.internal:5081`; if it is not running, banner ads are skipped
and the app still serves the core routes.

## Make targets

```
make            Start Jurassic + Postgres + api-server + web-server (alias: make up)
make lotr       Start only LOTR Postgres + api-server + web-server
make dev        Run both servers (assumes db is up)
make api        Run api-server only        → http://localhost:5030
make web        Run web-server only        → http://localhost:5292
make jurassic   Init/run Jurassic stack    → web http://localhost:5080, api http://localhost:5081
make db         Start Postgres + create dbs + apply web schema
make db-reset   Drop and recreate both databases (destructive)
make db-psql    Open psql against $(API_DB)
                  override with: make db-psql DB=lotr_users
make test       Run all tests (LotrApi.Tests requires Docker)
make down       Stop LOTR Postgres and the Jurassic stack
make clean      Stop containers and remove .NET build artifacts
make help       Print the target list
```

Override defaults inline if you need to, e.g. `make PG_PORT=5433 db`.
The full list of variables is at the top of the [Makefile](Makefile). For
example, to use a separate Jurassic checkout instead of the submodule, run
`make JURASSIC_DIR=/path/to/Project1-jurassic-park`.

## Configuration

Defaults that the Makefile and app config agree on:

| Setting           | Value                                   |
| ----------------- | --------------------------------------- |
| Postgres user/pw  | `postgres` / `postgres`                 |
| Postgres port     | `5432`                                  |
| API database      | `lotr`                                  |
| Users database    | `lotr_users`                            |
| LOTR API          | `http://localhost:5030`                 |
| Jurassic API      | `http://localhost:5081`                 |
| JWT shared secret | `Cool_Mega_Secret_Key_For_JWT_Token_Generation` (dev fallback) |

The shared JWT secret is the dev fallback baked into both
[web-server/Program.cs](web-server/Program.cs) and
[api-server/backend/src/LotrApi/Program.cs](api-server/backend/src/LotrApi/Program.cs).
Override in production via the `Jwt__Secret` environment variable, and override
the connection strings via `ConnectionStrings__DefaultConnection` (api) and
`ConnectionStrings__UsersConnection` (web). The MVC app's API dependencies can
also be overridden with `LotrApi__BaseUrl` and `JurassicApi__BaseUrl`.

## API endpoints

The API server is the source of truth for game data. All endpoints except
`/health` require a `Bearer` JWT signed with the shared secret. The web server
attaches the cookie token automatically when proxying requests.

| Method | Path                     | Description                                              |
| ------ | ------------------------ | -------------------------------------------------------- |
| GET    | `/health`                | API process liveness (no auth)                           |
| GET    | `/classes`               | All classes                                              |
| GET    | `/class/{id}`            | One class (`name`, `desc`, `racialids`)                  |
| GET    | `/class/{id}/abilities`  | Abilities scoped to a class                              |
| GET    | `/race`                  | All races (`id`, `name`, `modifiers`)                    |
| GET    | `/stats`                 | All stat definitions and base values                     |
| GET    | `/stats/{name}`          | Single stat by name (case-insensitive)                   |
| GET    | `/charhealth`            | Character health stat (key `charhealth`, **not** `health`) |
| GET    | `/strength`              | Strength stat                                            |
| GET    | `/abilities?class_id=N`  | Abilities, optionally filtered by class                  |
| GET    | `/premades`              | Premade characters with stats JSON                       |
| GET    | `/names`                 | Premade character names                                  |
| POST   | `/generate`              | Body: `{ "class_id": N, "race_id": N }` → rolled sheet (4d6 drop lowest, race modifiers, CON-modified `charhealth`) |

The full schema lives under
[api-server/backend/src/database/schema/](api-server/backend/src/database/schema/)
and is applied automatically by `DatabaseBootstrap` when the API server starts.
Premades and base stats are seeded by [006_seed.sql](api-server/backend/src/database/schema/006_seed.sql).

## Web server routes

| Path                         | Purpose                                          |
| ---------------------------- | ------------------------------------------------ |
| `/Auth/Login`, `/Auth/Register` | Login + register pages (anonymous)            |
| `/premade`                   | Premade character browser (auth required)        |
| `/character/create`          | Roll a new character (auth required)             |
| `/character/sheet`           | View a generated/saved sheet (auth required)     |
| `/my-characters`             | List of saved sheets (auth required)             |
| `/api/auth/{login,logout,register}` | JSON auth endpoints (sets `AuthToken` cookie) |
| `/api/character/sheets`      | `POST` save new sheet, `GET` list, `PUT /{id}` update, `DELETE /{id}` delete |
| `/api/character/{races,classes,abilities,stats,generate}` | Authenticated proxy to the API server |
| `/api/gamedata/*`            | Mirror endpoints over the same API client        |
| `/api/premade/{list,select/{id}}` | Premade browse + select                     |

## Character sheet editor

Saved character sheets are fully editable after creation. An **Edit** button appears on the sheet view for any sheet owned by the logged-in user (including premade characters that have been saved as a copy).

Clicking **Edit** enters edit mode, which converts the following read-only fields into inputs or textareas:

- Background, Player Name, Alignment
- Personality Traits, Ideals, Bonds, Flaws
- Equipment
- Features & Traits (user-authored text; class ability descriptions are appended automatically outside of edit mode)
- Attacks & Spellcasting (up to 6 rows: Name / Atk Bonus / Damage)

**Save** issues a `PUT /api/character/sheets/{id}` for existing sheets, or a `POST /api/character/sheets` when saving a premade as a new copy. On success the sheet view is repopulated from the returned data without a page reload.

**Premade characters** always show the Edit button. Selecting a premade and clicking Edit will save it as a new sheet in your account before applying changes, leaving the original premade untouched.

The three new columns are added by schema migrations applied automatically on `make db`:

| Migration                              | Change                                                               |
| -------------------------------------- | -------------------------------------------------------------------- |
| `004_add_sheet_equipment_features.sql` | Adds `equipment` and `features_traits` columns to `character_sheets` |
| `005_add_sheet_attacks.sql`            | Adds `attacks` JSONB column to `character_sheets`                    |

## Tests

The Makefile target runs both suites:

```bash
make test
```

Which is equivalent to:

```bash
dotnet test web-server.Tests/web-server.Tests.csproj
dotnet test api-server/backend/src/LotrApi.Tests/LotrApi.Tests.csproj
```

- The **web-server tests** use `WebApplicationFactory` with mocked services; no DB or Docker needed.
- The **API tests** use [Testcontainers](https://dotnet.testcontainers.org/) to spin up a throwaway `postgres:16-alpine` container per run, so **Docker must be running** or they'll fail.

CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs the API tests against a service-container Postgres on every push and pull request.

## Project layout

```
.
├── Makefile                       # Entry point — `make` runs everything
├── SPEC.md                        # Original design spec
├── frontend/                      # Static HTML pages served by web-server
├── web-server/                    # ASP.NET Core MVC user-facing app
│   ├── Controllers/               # Auth, Character, Premade, MyCharacters, GameData
│   ├── Services/                  # AuthService, CharacterSheetService, LotrApiClient
│   └── database/schema/           # users + character_sheets SQL (applied by `make db`)
├── web-server.Tests/              # xUnit + Moq endpoint tests
└── api-server/
    ├── backend/
    │   ├── LotrApi.slnx
    │   └── src/
    │       ├── LotrApi/           # Minimal API + DatabaseBootstrap
    │       ├── LotrApi.Tests/     # Testcontainers-backed integration tests
    │       └── database/          # init.sql + reset.sql + schema/*.sql (auto-applied on start)
    └── docs/                      # JIRA backlog + wireframes
```

## Troubleshooting

- **`make` fails with "port 5432 already in use"** — another Postgres is running. Either stop it, or set `PG_PORT=5433` and update the connection strings in `appsettings.json` to match.
- **`make test` fails with Testcontainers / Docker errors** — make sure Docker Desktop is running. The API tests need it; the web-server tests don't.
- **Login fails with "Invalid credentials"** — the seed only runs on the first start. If you reset the users DB (`make db-reset`), restart the web server so `SeedDefaultUserAsync` re-creates `admin` / `password`.
- **API returns 401 on every endpoint** — the JWT secret must match between the web and API servers. Both default to the same dev fallback, so this only happens if you've overridden `Jwt:Secret` in one but not the other.
- **Wipe everything and start over:** `make clean && make`.
