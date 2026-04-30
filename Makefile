# Makefile for the LOTR project. Run `make` to start everything.
# Requires Docker (for Postgres) and the .NET SDK.

PG_CONTAINER  ?= lotr-pg
PG_USER       ?= postgres
PG_PASSWORD   ?= postgres
PG_PORT       ?= 5432
API_DB        ?= lotr
WEB_DB        ?= lotr_users

API_DIR        = api-server/backend/src/LotrApi
WEB_DIR        = web-server
API_TEST_PROJ  = api-server/backend/src/LotrApi.Tests/LotrApi.Tests.csproj
WEB_TEST_PROJ  = web-server.Tests/web-server.Tests.csproj
WEB_SCHEMA_DIR = web-server/database/schema

DOCKER_EXEC = docker exec $(PG_CONTAINER) psql -U $(PG_USER)

.PHONY: default up dev api web db db-start db-wait db-create db-apply-web-schema \
        db-psql db-reset test down clean help

default: up

help:
	@echo "Targets:"
	@echo "  make            Start Postgres + api-server + web-server"
	@echo "  make up         Same as default"
	@echo "  make dev        Run both servers (assumes db is up)"
	@echo "  make api        Run api-server only (http://localhost:5030)"
	@echo "  make web        Run web-server only (http://localhost:5292)"
	@echo "  make db         Start Postgres + create dbs + apply web schema"
	@echo "  make db-reset   Drop and recreate both databases"
	@echo "  make db-psql    Open psql against \$$(API_DB) (override: make db-psql DB=lotr_users)"
	@echo "  make test       Run all tests (LotrApi.Tests requires Docker)"
	@echo "  make down       Stop and remove the Postgres container"
	@echo "  make clean      Stop containers and remove build artifacts"

up: db dev

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

down:
	-docker stop $(PG_CONTAINER) 2>/dev/null
	-docker rm $(PG_CONTAINER) 2>/dev/null

clean: down
	dotnet clean $(API_DIR)
	dotnet clean $(WEB_DIR)
