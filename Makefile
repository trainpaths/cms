.PHONY: install dev dev-build up down lint typecheck build-back build-front gen-api test-backend test-front test-e2e test

install:  ## pnpm workspace: frontend/ (@trainpaths/cms package) + playground/ (instance app)
	pnpm install

dev:  ## Start postgres + seaweedfs + api, then the playground's Vite dev server
	docker compose up -d postgres seaweedfs api && pnpm dev

dev-build:
	docker compose up -d --build postgres seaweedfs api && pnpm dev

up:
	docker compose up -d --build

down:
	docker compose down

build-back:
	dotnet build api-backend/api-backend.csproj

build-front:  ## playground build: vue-tsc (incl. the package source it imports) + client + SSR bundles
	pnpm build

gen-api: ## Regenerate the typed API client (frontend/src/api/, committed) from swagger.json
	pnpm gen-api

lint:
	pnpm lint

typecheck:
	pnpm typecheck

test-backend:
	dotnet test --project api-backend/Tests/Tests.csproj

test-front: ## Vitest unit tests (SSR render, head tags)
	pnpm test

test-e2e:
	pnpm test:e2e

test: test-backend test-front test-e2e
