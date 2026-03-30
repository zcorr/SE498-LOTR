DB_USER ?= $(shell whoami)
DB_NAME ?= lotr
TEST_DB_NAME ?= lotr_test
DB_HOST ?= localhost
DB_PORT ?= 5432
MAINTENANCE_DB ?= postgres
PSQL = psql -X -q -h $(DB_HOST) -p $(DB_PORT) -U $(DB_USER)
DB_DIR = api-server/backend/src/database
TEST_DIR = api-server/backend/tests

.PHONY: create-db reset-db run test-schema

create-db:
	@db_exists=`$(PSQL) -d $(MAINTENANCE_DB) -tAc "SELECT 1 FROM pg_database WHERE datname = '$(DB_NAME)'"`; \
	if [ "$$db_exists" != "1" ]; then \
		createdb -h $(DB_HOST) -p $(DB_PORT) -U $(DB_USER) $(DB_NAME); \
	fi

reset-db: create-db
	cd $(DB_DIR) && \
	$(PSQL) -d $(DB_NAME) -f reset.sql && \
	$(PSQL) -d $(DB_NAME) -f init.sql

run: create-db
	cd $(DB_DIR) && \
	$(PSQL) -d $(DB_NAME) -f init.sql

test-schema:
	@$(MAKE) reset-db DB_NAME=$(TEST_DB_NAME)
	$(PSQL) -d $(TEST_DB_NAME) -f $(TEST_DIR)/schema_creation_test.sql
