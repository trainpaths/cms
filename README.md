# CMS

A block-based content management system: site owners build pages from blocks in a Gutenberg-style editor; visitors
get server-rendered pages at `/{slug}`.

This repository is the **framework**. A client site is its own small repo (start from the template
[`trainpaths/cms-starter`](https://github.com/trainpaths/cms-starter)) that imports the CMS and adds only what is
specific to it: blocks, page templates, component overrides, a theme and a config file. Upgrading a site means
bumping one version.

- **Frontend**: the source package `@trainpaths/cms` (`frontend/`), installed from a git tag:
  `"@trainpaths/cms": "github:trainpaths/cms#vX.Y.Z&path:/frontend"`
- **API**: the image `ghcr.io/trainpaths/cms-api:X.Y.Z`, configured per site by `cms.config.json`

Building a site on it: [`frontend/docs/INSTANCE_GUIDE.md`](frontend/docs/INSTANCE_GUIDE.md).

## Features

- Block editor: nested blocks, drag & drop, undo/redo, autosave, outline, responsive (container queries)
- Pages: drafts, publish, tags, search, slugs, per-page meta title/description, preview
- Public site: server-rendered on content change, hydrated, link-preview tags, client-side navigation
- Media library (S3-compatible storage), menus (nested, page links resolved by id), site configuration (fields and
  groups such as contact and socials, shaped by the site's developer), logo / icon / share image
- Per site: own blocks, template pages (e.g. a legal notice built from the configuration), overrides, theme, optional
  customer accounts
- Staff accounts with JWT auth (access token + rotating refresh cookie), rate limiting, strict CSP

## Stack

| Layer      | Tech                                                                                 |
| ---------- | ------------------------------------------------------------------------------------ |
| Frontend   | Vue 3 (`<script setup>`), TypeScript, Tailwind v4, Vite, vue-router, Pinia, [nb-ui](https://github.com/trainpaths/nb-ui) |
| API client | hey-api, generated from the API's OpenAPI contract (both committed)                  |
| Backend    | C# .NET 10, EF Core (migrations auto-apply on startup), JWT auth                     |
| Database   | PostgreSQL 18 (page block trees in `jsonb`)                                          |
| Media      | SeaweedFS (S3 API), streamed by the API                                              |
| Serving    | nginx (admin SPA, `/api` proxy, pre-rendered public pages); Node renderer (Vue SSR)  |

## Repo layout

```
frontend/          @trainpaths/cms package (admin, editor, public site, vite plugin, renderer) → frontend/CLAUDE.md
  docs/              guide for building sites on the CMS (ships in the package)
playground/        an instance app using the package via the workspace; dev server, compose stack, e2e → playground/CLAUDE.md
api-backend/       C# .NET 10 API (+ xUnit / Testcontainers tests) → api-backend/CLAUDE.md
claude-context/    design docs (overview, architecture decisions, block system)
compose.yaml       full local stack (postgres + seaweedfs + api + renderer + frontend), ports on 127.0.0.1
```

## Developing the CMS

```bash
cp .env.example .env     # set API_JWT_KEY, S3_SECRET_KEY (openssl rand -hex 32), BOOTSTRAP_SUPERADMIN_* (first admin)
pnpm install
pnpm docker:up                    # full stack → http://localhost:5173, admin at /admin/login
pnpm dev                          # postgres + seaweedfs + api in docker, playground Vite dev server with hot reload
```

| Script                           | What it does                                                                          |
| -------------------------------- | ------------------------------------------------------------------------------------- |
| `pnpm dev`                       | API stack in docker + playground Vite dev server (`dev:build` rebuilds the API image) |
| `pnpm docker:up` / `docker:down` | Start / stop the full stack                                                           |
| `pnpm build:back`                | Build the API (rewrites the committed OpenAPI contract)                               |
| `pnpm gen-api`                   | Regenerate the committed typed client from the contract                               |
| `pnpm lint`                      | ESLint (package + playground)                                                         |
| `pnpm typecheck`                 | vue-tsc (package + playground)                                                        |
| `pnpm build:front`               | Build the playground (client + SSR bundles)                                           |
| `pnpm test:back`                 | `dotnet test` (xUnit + Testcontainers)                                                |
| `pnpm test:front`                | Vitest (SSR render, head tags)                                                        |
| `pnpm test:e2e`                  | Playwright against the running stack (see `playground/CLAUDE.md`)                     |
| `pnpm test:all`                  | All three test suites                                                                 |

API change: `pnpm build:back`, then `pnpm gen-api`, commit `frontend/openapi/swagger.json` and `frontend/src/api/`;
CI fails when either drifts.

## Releases

Every green CI run on `main` may release: the version bump comes from the Conventional Commits since the last tag
(`feat` → minor, `fix` etc. → patch, `!` / `BREAKING CHANGE` → major; docs/chore/ci/test/style/build only → none).
The release tags `vX.Y.Z` (the package version) and publishes `ghcr.io/trainpaths/cms-api:X.Y.Z`.

## Docs

- Building a site: [`frontend/docs/INSTANCE_GUIDE.md`](frontend/docs/INSTANCE_GUIDE.md)
- Architecture & decisions: [`claude-context/`](claude-context/)
- Package internals: [`frontend/CLAUDE.md`](frontend/CLAUDE.md), block editor:
  [`frontend/src/lib/web-editor/CLAUDE.md`](frontend/src/lib/web-editor/CLAUDE.md)
- API internals: [`api-backend/CLAUDE.md`](api-backend/CLAUDE.md)

## License

[MIT](LICENSE)
