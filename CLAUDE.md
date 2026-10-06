# CLAUDE.md — CMS

A block-based CMS: website owners (staff accounts) build pages in a block editor;
published pages are public at `/{slug}`. Built on the vue-csapi-template (Vue 3 SPA + C# .NET 10 API +
PostgreSQL, Customer + Staff JWT auth). The block editor was ported from the standalone
`gutenberg-copy` (NuxtBlocks) project.

**This repo is the framework.** Client sites are separate *instance* repos that import it (like nb-ui):
the frontend as the source package `@trainpaths/cms` (`frontend/`), the API as a prebuilt image. An instance
adds its own blocks, templates, overrides, theme and `cms.config.json`; `playground/` is the in-repo instance used
for development and e2e. Why/how: `claude-context/ARCHITECTURE.md` → Framework and instances. Split tracked in issue #63.

## Context docs (read before implementing)
`claude-context/` holds the design docs — read the relevant one first:
1. **`PROJECT_OVERVIEW.md`** — what the CMS is, who uses it, feature map, roadmap/open items
2. **`ARCHITECTURE.md`** — key decisions (Postgres jsonb instead of Mongo, auth model, slugs, limits) and why
3. **`BLOCK_SYSTEM.md`** — block data model, registry, editor/view split, **how to add a new block type**

`frontend/docs/INSTANCE_GUIDE.md` is the guide for **instance developers** (ships in the package): keep it current
whenever config keys, extension points, stable override points, exports or the Docker setup change.

Per-directory quick references (keep them current when you change those areas):
```
frontend/CLAUDE.md                    the @trainpaths/cms package: entries, vite plugin, routes, stores, nb-ui, theme
frontend/src/lib/web-editor/CLAUDE.md  block editor library (the biggest part of the frontend)
playground/CLAUDE.md                  dev instance app + e2e suite
api-backend/CLAUDE.md                 .NET API: auth, pages, config, tests, OpenAPI generation
```

## Tech stack
| Layer      | Tech |
|------------|------|
| Frontend   | Vue 3 (`<script setup>`), TypeScript, Vite, vue-router, Pinia, Tailwind v4 (1 spacing unit = 1px) |
| UI kit     | `@trainpaths/nb-ui` (own public repo, installed from a git tag; re-exported as `@trainpaths/cms/ui`) |
| API client | hey-api, generated from the committed `frontend/openapi/swagger.json` into the committed `frontend/src/api/` |
| Backend    | C# .NET 10 controllers, EF Core 10 + Npgsql, JWT bearer |
| Database   | PostgreSQL 18 (auth tables, `pages` with the block tree in a `jsonb` column, `tags`/`page_tags`, `menus` (jsonb item tree), `media_assets`, `site_config`, `backup_settings`) |
| Backups    | API runs `pg_dump`/`pg_restore`/`psql` (client 18 in the api image): DB + media archives in `/backups` (`BACKUP_DIR`), see ARCHITECTURE.md → Backups |
| Media      | SeaweedFS (S3 API, internal only); the API streams files at `/api/public/media/{key}` |
| Serving    | nginx (frontend container): admin SPA, `/api` reverse proxy, public pages via the API's stored HTML |
| Public SSR | `renderer` container (Node, `frontend/server/render-server.js`): Vue SSR of public pages on content change; see ARCHITECTURE.md → Public pages |

## Layout
```
claude-context/          design docs (see above)
frontend/                @trainpaths/cms package (raw source): admin SPA, public site, editor, built-in blocks,
                         vite plugin, server/ (renderer, dev SSR), nginx.conf, generated API client
  src/lib/web-editor/     block editor library (editor, blocks, view renderer, limits)
playground/              instance app on `workspace:*` (cms.config.json, index.html, public.html, entry stubs, style.css,
                         sample block/override/template) + e2e/
api-backend/             .NET API (+ Tests/: xUnit unit + Testcontainers integration)
package.json, pnpm-workspace.yaml   pnpm workspace root (frontend + playground; ESLint, all repo scripts)
Dockerfile               5-stage build: api-build → frontend-build (playground) → api → renderer → frontend
compose.yaml             postgres + seaweedfs (internal `backend` network) + api + renderer + frontend (the playground
                         instance); ports on 127.0.0.1 only; `backups-init` chowns the backups bind mount
.env.example             copy to .env (git-ignored)
```

## Commands
```bash
cp .env.example .env     # set API_JWT_KEY + S3_SECRET_KEY (openssl rand -hex 32) + BOOTSTRAP_SUPERADMIN_* (first staff login)
pnpm install             # workspace
pnpm docker:up           # full stack (playground) → http://localhost:5173; docker:down stops
pnpm dev                 # postgres + seaweedfs + api in docker, playground Vite dev server with hot reload
                         # (dev:build rebuilds the api image first; dev:web = Vite only)
pnpm build:back          # dotnet build → regenerates frontend/openapi/swagger.json (commit it; CI checks drift)
pnpm gen-api             # regenerate the typed client frontend/src/api/ from swagger.json (commit it; CI checks drift)
pnpm lint                # ESLint (package + playground)
pnpm typecheck           # vue-tsc: package + playground
pnpm build:front         # playground type-check + build (client + SSR bundles)
pnpm test:back           # xUnit (needs Docker for Testcontainers)
pnpm test:front          # Vitest unit tests (SSR render)
pnpm test:e2e            # Playwright (needs the stack running; editor specs need staff creds, see frontend/CLAUDE.md)
pnpm test:all            # test:back + test:front + test:e2e
```

**No local .NET SDK?** Run `dotnet` in the SDK image with the repo mounted at the same path (the
Testcontainers tests also need the Docker socket):
```bash
docker run --rm --network host -v "$PWD":"$PWD" -w "$PWD" \
  -v /var/run/docker.sock:/var/run/docker.sock --group-add "$(stat -c %g /var/run/docker.sock)" \
  --user "$(id -u):$(id -g)" -e HOME=/tmp/dnhome -e DOTNET_CLI_HOME=/tmp/dnhome \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet test --project api-backend/Tests/Tests.csproj
```
(`dotnet ef` needs `dotnet tool install --global dotnet-ef --version 10.0.12` inside the container first.)
`BackupRoundTripTests` need `pg_dump`/`pg_restore`/`psql` 18 and are skipped without them (the plain SDK image has
none; CI installs them): build an SDK image with `postgresql-client-18` (same apt lines as the Dockerfile's `api`
stage) and run the command above with it.

## .env vars
`.env` is read by docker compose (`${VAR}` interpolation) and Vite (`loadEnv`). Host-side
`dotnet run` does **not** read it; it uses `appsettings*.json`. Postgres and SeaweedFS are not exposed to the host and
sit on the internal `backend` network (only the api joins both). Production: a reverse proxy on the host → `127.0.0.1:FRONTEND_PORT`
(nginx trusts its X-Forwarded-For, see ARCHITECTURE.md → Networking).
```
API_PORT=5183 / FRONTEND_PORT=5173     host ports (bound to 127.0.0.1; API_PORT only matters for `pnpm dev`)
DB_NAME, DB_USER, DB_PASSWORD          postgres + api connection string
ASPNETCORE_ENVIRONMENT                 Production (default) | Development
CORS_ORIGINS                           → Cors:Origins
API_JWT_KEY (required, ≥32 chars), API_JWT_ISSUER/AUDIENCE/ACCESS_MINUTES/REFRESH_DAYS  → Jwt:*
RATE_LIMIT_PERMIT/WINDOW_SECONDS/QUEUE           → RateLimiting:*          (credential endpoints)
RATE_LIMIT_GENERAL_PERMIT/WINDOW_SECONDS         → RateLimiting:General:*  (session endpoints)
RATE_LIMIT_PAGES_PERMIT/WINDOW_SECONDS           → RateLimiting:Pages:*    (editor + public pages, 600/min)
FORWARDED_KNOWN_NETWORKS               → ForwardedHeaders:KnownNetworks
AUTH_SECURE_COOKIE                     → Auth:SecureCookie
BOOTSTRAP_SUPERADMIN_EMAIL/PASSWORD    first staff account (= site owner who can use the editor)
S3_ACCESS_KEY/S3_SECRET_KEY (required), S3_BUCKET   SeaweedFS admin identity → Storage:S3:* (endpoint fixed: http://seaweedfs:8333)
BACKUP_DIR                             host folder for backup archives (→ /backups in the api; empty = ./backups)
SMTP_*, EMAIL_FROM, EMAIL_FROM_NAME, APP_BASE_URL   email (empty SMTP_HOST → console sender); APP_BASE_URL is also
                                       the public origin in canonical/OG tags (→ Renderer:PublicBaseUrl)
VITE_API_BASE_URL                      Vite dev proxy target only (http://127.0.0.1:API_PORT; the port is IPv4-only)
```

## Data flow
Browser → nginx (or Vite in dev) → `/api/*` → .NET API → PostgreSQL (+ SeaweedFS for media bytes).
Public pages: nginx → API `/api/public/html/{slug}` → HTML pre-rendered by the `renderer` (Vite dev renders them itself).
Typed client generated from the committed contract (and committed itself: instances don't run hey-api); EF migrations auto-apply on API startup
(skipped during build-time OpenAPI generation, detected via the `GetDocument.Insider` host).

## Code style
- **Indentation: tabs everywhere** (`.editorconfig`; YAML uses spaces). EF-generated `Migrations/` keep their spaces.
- **C#**: Multi-line initializers, collection expressions, enums, anonymous
  objects, switch expressions: trailing comma on the last item, closing bracket on its own line.
  Parameter/argument lists (records, primary ctors, calls) can't take a trailing comma, so their `)`
  stays on the last item's line.
- **Frontend**: tabs, ESLint (`pnpm lint`); multi-line lists end with a trailing comma.

## CI + releases (.github/workflows/)
`ci.yml` runs on PRs + pushes to `main`. **backend**: vulnerable-package check, `dotnet test`,
OpenAPI drift check. **frontend**: `pnpm audit`, generated-client drift check, lint, typecheck, Vitest, compose stack,
Playwright from `playground/` (with a bootstrap super admin for the editor specs). Dependabot opens weekly update PRs.
The backend job installs `postgresql-client-18` (backup round-trip tests).
`release.yml` runs after a green CI run on `main` (or manually, forcing the bump): Conventional Commits since the last
tag → bump (`!`/`BREAKING CHANGE` major, `feat` minor, else patch; only docs/chore/ci/test/style/build → none), commits
`chore(release): vX.Y.Z` (version in `frontend/package.json`) **on the tag only**, GitHub release, pushes
`ghcr.io/trainpaths/cms-api:X.Y.Z` / `:X.Y` / `:latest` (amd64 + arm64; build arg `CMS_VERSION` → `Cms:Version`, recorded in
backup manifests). `main` is protected (PR + required checks)
and never gets the release commit: its `frontend/package.json` version is stale, tags are authoritative.
**Breaking for instances** (config keys, override points, block contract, entry stubs) → mark the commit `!`.

## Git workflow
- **`main`** = default + integration branch; every green push may release (see above). No deployment branch here:
  client sites deploy from their own instance repos (`trainpaths/cms-starter` template).
- **Before starting any work, sync with remote**: `git fetch origin --prune && git checkout main && git pull`,
  then branch from the fresh `main`. Local refs are often stale (the user merges/pushes from elsewhere).
- Feature branches off `main` (`feat/...`, `fix/...`) → PR into `main` (merge, not rebase) → delete branch.
  Put `Closes #N` in the PR body; merging into the default branch auto-closes the issue.
- Work is tracked in GitHub issues (plan as a checkbox list; tick items off; close when done).

**Conventional Commits**, short single line: `<type>(<scope>): <description>`
- Types: `feat`, `fix`, `refactor`, `style`, `docs`, `test`, `chore`
- Scopes: `editor`, `blocks`, `ui`, `store`, `api`, `auth`, `db`, `infra`, `dep`
