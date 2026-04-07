# 498 Lord of the Rings Board Game

| | |
|---|---|
| **Authors** | Zack Corr, Kylee Down, Ivan Dombiak |
| **Team** | Team X |
| **Reviewers** | Zack Corr, Kylee Down, Ivan Dombiak, Tom DuPont |
| **Created On** | February 23rd, 2026 |
| **Last Updated** | March 31st, 2026 |
| **Jira** | [Link here](#https://lotrbg.atlassian.net/jira/software/projects/SCRUM/boards/1) |

---

## Table of Contents

1. [Introduction](#introduction)
2. [Technical Requirements](#technical-requirements)
3. [Functional Requirements](#functional-requirements)
4. [Non-Functional Requirements](#non-functional-requirements)
5. [Out of Scope / Non-Goals](#out-of-scope--non-goals)
6. [Solutions](#solutions)
7. [Testing](#testing)
8. [Alternative Solutions](#alternative-solutions)

---

## Introduction :)

This project is a web-based **Lord of the Rings themed board game character sheet manager**. Users can log in, create new character sheets, or select from premade characters (e.g., Aragorn, Gandalf).

Character data includes class, race, abilities, and stats. It is generated and stored via a **two-server architecture**: a C# MVC Web Server handling user sessions, and a separate C# Web API server managing in-game data and character generation.

---

## Technical Requirements

### Frontend
- HTML, CSS, JS, Bootstrap CSS

### Web Server
- C# MVC (Model View Controller)
- PostgreSQL (users + sheets)
- Basic Auth for login
- Bearer Token for subsequent requests

### API Server
- C# Web API
- PostgreSQL (game data)
- REST endpoints
- Bearer token auth

### Communication Structure

```
Frontend ←→ Web Server (RPC) ←→ API Server (REST)
```

### Auth Standard
- OAuth2 *(not enforced)*

### Database
- PostgreSQL on both servers

---

## Functional Requirements

### Frontend
- Built with HTML, CSS, JS, Bootstrap
- Two main pages: New character sheet, Premade character sheet selector
- Login page using plaintext user/password (Basic Auth)
- Once authenticated, all following requests use Bearer Tokens
- Web server exposes a view for character sheets

### Web Server (C# MVC)
- Receives HTML/RPC requests from frontend using Basic Auth on first login
- Issues and validates Bearer Tokens for all following requests
- Reads/writes to DB storing users and associated sheets
- Acts as the **middleware layer**
- Forwards character data to API Server via REST using Bearer Token

### API Server (C# Web API)

10 exposed REST endpoints:

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/class/{id}` | Returns class data (name, desc, race_id, etc.) |
| `GET` | `/stats` | Returns character stat values |
| `GET` | `/health` | Returns API server health (process / liveness) |
| `GET` | `/charhealth` | Returns character health values |
| `GET` | `/strength` | Returns character strength values |
| `GET` | `/abilities` | Returns character abilities list |
| `GET` | `/race` | Returns character race |
| `GET` | `/premades` | Returns premade sheets |
| `GET` | `/names` | Returns available character names |
| `POST` | `/generate` | Returns a fully completed character sheet |

The API server reads/writes to its own DB containing: Classes, Abilities, Race, Stats, and Premades.

**Example response for `GET /class/1`:**

```json
{
  "name": "Cool Guy",
  "desc": "Cool Guy is a vicious barbarian originating from Mordor",
  "racialids": [4]
}
```

---

## Non-Functional Requirements

- API response time must not exceed **1 second**
- Auth follows OAuth2 *(no longer required)*
- Bearer Tokens must be used for all communication between Web Server ↔ API Server

---

## Out of Scope / Non-Goals

- No multiplayer
- No game loop or board game mechanics
- No third-party OAuth providers (e.g., Google SSO)
- No focus on mobile / cross-browser compatibility

---

## Solutions

### Current Solution
No existing solution, greenfield project.

### Proposed Solution
Three-tier architecture: **Frontend → Web Server → API Server**

### Schema

**Web Server DB:**

| Table | Columns |
|-------|---------|
| `Users` | id, username, pw_hash, created_at |
| `Sheets` | id, user_id, character_name, class_id, race_id, stats (JSON), created_at |

**API Server DB:**

| Table | Columns |
|-------|---------|
| `Classes` | id, name, desc |
| `Abilities` | id, name, desc, class_id |
| `Race` | id, name, modifiers |
| `Stats` | id, name, base_value |
| `Premades` | id, name, class_id, race_id, stats (JSON) |

**Naming (health):** `GET /health` is **server** liveness only. Character health is **`GET /charhealth`**. In data, use stat name **`charhealth`** in `Stats` and key **`charhealth`** in `Premades.stats` JSON (not `health`).

### Business Logic

**On Login:**
```
Frontend (Basic Auth) → Web Server validates → Issues Bearer Token
```

**On Character Creation:**
```
Frontend → Web Server → API Server (Bearer Token) → DB query → Return sheet → Frontend
```

**On Premades:**
```
Fetch directly from API Server premades table
```

### Presentation Layer
- Bootstrap CSS for frontend
- Two key views: New character sheet form & premade sheet selector
- Print view exposed via the Web Server for physical/PDF character sheet output

---

## Testing

- **Unit tests** on API endpoints (verify response shape and response time < 1s)
- **Integration tests** on Web Server ↔ API Server auth flow (Bearer Token validation)
- **Manual UI testing** on login flow and character sheet creation

---

## Alternative Solutions

### Single Server Architecture
- Bundles Web Server and API Server, as well as DBs
  - SRP and separation of concerns becomes a problem

### Third-Party Auth Providers
- Auth0, Okta instead of custom Bearer Tokens
  - Adds external dependency
  - Costs money