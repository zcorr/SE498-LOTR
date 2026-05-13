# Makefile for the LOTR project. Run `make` to start everything.
# Requires Docker (for Postgres) and the .NET SDK.

PG_CONTAINER  ?= lotr-pg
PG_USER       ?= postgres
PG_PASSWORD   ?= postgres
PG_PORT       ?= 5432
API_DB        ?= lotr
WEB_DB        ?= lotr_users
JURASSIC_SUBMODULE_PATH = external/Project1-jurassic-park
JURASSIC_DIR  ?= $(CURDIR)/$(JURASSIC_SUBMODULE_PATH)
JURASSIC_WEB_PORT       ?= 5080
JURASSIC_MOVIE_PORT     ?= 5081
JURASSIC_POSTGRES_PORT  ?= 55433

API_DIR        = api-server/backend/src/LotrApi
WEB_DIR        = web-server
API_TEST_PROJ  = api-server/backend/src/LotrApi.Tests/LotrApi.Tests.csproj
WEB_TEST_PROJ  = web-server.Tests/web-server.Tests.csproj
WEB_SCHEMA_DIR = web-server/database/schema
JURASSIC_COMPOSE = $(JURASSIC_DIR)/docker-compose.yml

DOCKER_EXEC = docker exec $(PG_CONTAINER) psql -U $(PG_USER)
DOCKER_COMPOSE ?= docker compose

.PHONY: default up lotr dev api web jurassic jurassic-init db db-start db-wait db-create db-apply-web-schema \
        db-psql db-reset test down dev-down jurassic-down clean help

API_PORT ?= 5030
WEB_PORT ?= 5292

default: up

help:
	@echo "Targets:"
	@echo "  make            Start Jurassic + Postgres + api-server + web-server"
	@echo "  make up         Same as default"
	@echo "  make lotr       Start only LOTR Postgres + api-server + web-server"
	@echo "  make dev        Run both servers (assumes db is up)"
	@echo "  make api        Run api-server only (http://localhost:5030)"
	@echo "  make web        Run web-server only (http://localhost:5292)"
	@echo "  make jurassic   Start Jurassic movie service stack (web http://localhost:$(JURASSIC_WEB_PORT), api http://localhost:$(JURASSIC_MOVIE_PORT))"
	@echo "  make db         Start Postgres + create dbs + apply web schema"
	@echo "  make db-reset   Drop and recreate both databases"
	@echo "  make db-psql    Open psql against \$$(API_DB) (override: make db-psql DB=lotr_users)"
	@echo "  make test       Run all tests (LotrApi.Tests requires Docker)"
	@echo "  make down       Stop dev servers, LOTR Postgres, and the Jurassic stack"
	@echo "  make clean      Stop containers and remove build artifacts"

up: jurassic db dev

# Start only the LOTR-owned stack. Use this when Jurassic is not needed or is
# supplied separately by another group.
lotr: db dev

# Run api-server and web-server in parallel. Trap forwards Ctrl-C to both.
dev:
	@echo ">> starting api-server (:5030) and web-server (:5292) — Ctrl-C to stop"
	@trap 'kill 0' INT TERM EXIT; \
	  ( cd $(API_DIR) && dotnet run ) & \
	  ( cd $(WEB_DIR) && dotnet run ) & \
	  wait

api:
	cd $(API_DIR) && dotnet run

web:
	cd $(WEB_DIR) && dotnet run

jurassic: export WEB_HOST_PORT       = $(JURASSIC_WEB_PORT)
jurassic: export MOVIE_API_HOST_PORT = $(JURASSIC_MOVIE_PORT)
jurassic: export POSTGRES_HOST_PORT  = $(JURASSIC_POSTGRES_PORT)
jurassic:
	@$(MAKE) --no-print-directory jurassic-init
	$(DOCKER_COMPOSE) -f "$(JURASSIC_COMPOSE)" up --build -d postgres movieservice web
	@echo ">> Jurassic ready: web http://localhost:$(JURASSIC_WEB_PORT)  api http://localhost:$(JURASSIC_MOVIE_PORT)"

jurassic-init:
	@if [ ! -f "$(JURASSIC_COMPOSE)" ] && [ "$(JURASSIC_DIR)" = "$(CURDIR)/$(JURASSIC_SUBMODULE_PATH)" ]; then \
	  echo ">> initializing Jurassic submodule"; \
	  git submodule update --init --recursive -- "$(JURASSIC_SUBMODULE_PATH)"; \
	fi
	@if [ ! -f "$(JURASSIC_COMPOSE)" ]; then \
	  echo "Jurassic compose file not found at $(JURASSIC_COMPOSE). Run 'git submodule update --init --recursive' or override with: make JURASSIC_DIR=/path/to/Project1-jurassic-park"; \
	  exit 1; \
	fi

db: db-start db-wait db-create db-apply-web-schema

db-start:
	@if [ -z "$$(docker ps -q -f name=^$(PG_CONTAINER)$$)" ]; then \
	  if [ -n "$$(docker ps -aq -f name=^$(PG_CONTAINER)$$)" ]; then \
	    docker start $(PG_CONTAINER) > /dev/null; \
	    echo ">> started existing container $(PG_CONTAINER)"; \
	  else \
	    docker run -d --name $(PG_CONTAINER) \
	      -e POSTGRES_USER=$(PG_USER) \
	      -e POSTGRES_PASSWORD=$(PG_PASSWORD) \
	      -p $(PG_PORT):5432 \
	      postgres:16-alpine > /dev/null; \
	    echo ">> created container $(PG_CONTAINER)"; \
	  fi \
	else \
	  echo ">> $(PG_CONTAINER) already running"; \
	fi

db-wait:
	@until docker exec $(PG_CONTAINER) pg_isready -U $(PG_USER) > /dev/null 2>&1; do sleep 1; done

db-create:
	@for db in $(API_DB) $(WEB_DB); do \
	  exists=$$($(DOCKER_EXEC) -tAc "SELECT 1 FROM pg_database WHERE datname='$$db'"); \
	  if [ "$$exists" != "1" ]; then \
	    $(DOCKER_EXEC) -c "CREATE DATABASE $$db" > /dev/null; \
	    echo ">> created database $$db"; \
	  fi; \
	done

db-apply-web-schema:
	@for f in $(WEB_SCHEMA_DIR)/*.sql; do \
	  docker exec -i $(PG_CONTAINER) psql -U $(PG_USER) -d $(WEB_DB) -q < "$$f"; \
	done

db-psql:
	docker exec -it $(PG_CONTAINER) psql -U $(PG_USER) -d $(or $(DB),$(API_DB))

db-reset:
	@for db in $(API_DB) $(WEB_DB); do \
	  $(DOCKER_EXEC) -c "DROP DATABASE IF EXISTS $$db" > /dev/null; \
	done
	@$(MAKE) --no-print-directory db-create db-apply-web-schema

test:
	dotnet test $(WEB_TEST_PROJ)
	dotnet test $(API_TEST_PROJ)

down: dev-down
	-docker stop $(PG_CONTAINER) 2>/dev/null
	-docker rm $(PG_CONTAINER) 2>/dev/null
	-$(MAKE) --no-print-directory jurassic-down

# Stop anything listening on the api/web dev ports. SIGTERM first so ASP.NET
# Core can run its shutdown hooks (drain requests, dispose DB connections),
# then poll up to GRACE_SECONDS before falling back to SIGKILL.
GRACE_SECONDS ?= 10

dev-down:
	@for port in $(API_PORT) $(WEB_PORT); do \
	  pids=$$(lsof -ti tcp:$$port 2>/dev/null); \
	  if [ -n "$$pids" ]; then \
	    echo ">> stopping :$$port (pids $$pids)"; \
	    kill $$pids 2>/dev/null || true; \
	  fi; \
	done
	@for port in $(API_PORT) $(WEB_PORT); do \
	  for i in $$(seq 1 $(GRACE_SECONDS)); do \
	    pids=$$(lsof -ti tcp:$$port 2>/dev/null); \
	    [ -z "$$pids" ] && break; \
	    sleep 1; \
	  done; \
	  pids=$$(lsof -ti tcp:$$port 2>/dev/null); \
	  if [ -n "$$pids" ]; then \
	    echo ">> :$$port did not exit in $(GRACE_SECONDS)s, force-killing (pids $$pids)"; \
	    kill -9 $$pids 2>/dev/null || true; \
	  fi; \
	done

jurassic-down:
	@if [ -f "$(JURASSIC_COMPOSE)" ]; then \
	  $(DOCKER_COMPOSE) -f "$(JURASSIC_COMPOSE)" down; \
	fi

clean: down
	dotnet clean $(API_DIR)
	dotnet clean $(WEB_DIR)
