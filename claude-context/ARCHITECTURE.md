# Architecture & Decisions

## Framework and instances
A developer setting up a site for a client controls blocks, the public look, fixed pages, site config fields and
public auth, but shouldn't own (or fork) the whole CMS. So this repo is a **framework** and every client site is an
**instance repo** importing it, the nb-ui model (split planned in issue #63; GitHub template `trainpaths/cms-starter`).
- **Frontend = source package** `@trainpaths/cms` (`frontend/`), installed from a git tag of this repo with
  `github:trainpaths/cms#vX.Y.Z&path:/frontend` (pnpm `path:` = git subdirectory). No build step: the instance's Vite
  compiles it. The instance owns `index.html`, `public.html`, `vite.config.ts` (`plugins: [cms()]`) and three entry
  stubs; everything else (admin SPA, editor, public app, renderer, nginx.conf) comes from the package.
- **API = prebuilt image** (`ghcr.io/trainpaths/cms-api:X.Y.Z`, Dockerfile target `api`): instances write no C#.
  Possible because the backend doesn't know block types (see Block contract): new blocks are frontend-only.
- **Renderer is built per instance**: its SSR bundle must contain the instance's blocks and overrides.
- **One version**: package tag = image tag (they share the OpenAPI contract). The generated API client is committed
  in the package, so instances don't need hey-api. nb-ui is a peer dependency pinned to a git tag: pnpm 12 blocks
  git-hosted sub-dependencies, so each instance lists it directly (same tag).
- **Extension points**: `src/blocks/<folder>/` (new or replacing built-in blocks), `src/templates/<name>.vue`
  (template pages), `src/overrides/<Name>.vue` (replaces the package's component of that name, resolved by `cms()`),
  `src/overrides/ui/` (nb-ui), `src/style.css` (site theme). Overriding internals couples an instance to file
  names: only SiteHeader, SiteFooter, NotFound, PageContent, block Edit/View and nb-ui components count as stable
  (renames there = major version). Instance code imports `@trainpaths/cms/editor` (edit side) / `/site` (views).
- **Instance config** `cms.config.json` (`publicAuth`, `blocks.exclude`, `pages`), validated strictly on API start.
  The API is its only runtime reader (no frontend/backend default drift; `site.lang` is the one key the `cms()`
  Vite plugin applies at build time); the admin reads `/api/public/instance`. Pages it
  declares are seeded; their flags (template, locked, editable, footer) are **looked up by slug, not stored**: no
  migration, and dropping a page from the config just unlocks it. Locked (or template) = fixed slug, undeletable,
  re-seeded when missing. Templates render the page in the public app instead of the default layout and can read the
  site config (e.g. a legal page showing the owner's address); `editable: false` = template only, no blocks.
- **Site theme scope**: the instance overrides tokens under `.site-theme` (public root, preview, editor canvas), so the
  editor stays WYSIWYG while the admin keeps the CMS look; in-canvas chrome uses `cms-*` tokens resolved at :root.
- **Upgrades**: bump the package tag and the image tag together; migrations auto-apply on API start, so an instance
  database can't be downgraded.
- `playground/` is this repo's own instance (workspace link): dev server, compose stack and e2e run against it.

## Storage: PostgreSQL `jsonb`, not MongoDB
gutenberg-copy stored pages in MongoDB. The template's auth (EF Core migrations, Testcontainers
Postgres tests, seeded roles) is built on Postgres, so running both databases would double the ops
cost for no real gain. Decision: **Postgres only**.

- Table `pages`: `Id`, `Title`, `Slug` (unique), `Status` (`Draft`|`Published`, stored as string),
  `Blocks` (**jsonb**, the whole tree), `CreatedAt`, `UpdatedAt`, `PublishedAt`, `CreatedById`/`UpdatedById` (FK → staff, SET NULL).
- `Blocks` is mapped with a System.Text.Json **value converter** (camelCase, Web defaults) + ValueComparer,
  not Npgsql's dynamic JSON mapping — keeps it working with the EF InMemory provider used by unit tests.
- Size: a jsonb value can be up to ~255 MB and large values are TOAST-compressed out of line; real pages
  are KBs. Every save rewrites the whole column (same as the Mongo version did).
- Binary media never goes into jsonb — blocks store a media **id**; the bytes live in blob storage (see Media).

## Media: SeaweedFS (S3 API) behind the API
Chosen over MinIO (community edition archived Apr 2026, images pulled), RustFS (1.0 GA only Sept 2026)
and Garage (AGPL, no bucket policies/versioning): SeaweedFS is Apache 2.0, mature, one container, light.
- compose service `seaweedfs` (`server -s3`, volume `seaweed-data`). **Not published to the host**, on the internal
  `backend` network (see Networking); `-ip.bind=0.0.0.0` only means all interfaces of its own container;
  S3 credentials come from `S3_ACCESS_KEY`/`S3_SECRET_KEY` (→ SeaweedFS admin identity), anonymous S3
  access is denied. The master/filer/volume ports have no auth, so it must stay on the internal network.
- The API is the only client (`IBlobStorage` → `S3BlobStorage`, AWSSDK.S3, path-style; bucket created on
  first write). Any S3 store (R2, AWS, Azure via gateway) works by changing `Storage:S3:*` only.
- Files are served by the API at `GET /api/public/media/{guid}.{ext}` (anonymous, immutable 1-year cache,
  `CSP: default-src 'none'; sandbox`), so images are **same-origin**: no CSP change, no presigned URLs,
  and the store is never reachable from outside.
- Table `media_assets`: `Id`, `StorageKey` (`{id:N}.{ext}`, unique), `FileName`, `ContentType`, `Size`,
  **`Alt`**, `CreatedAt`, `CreatedById` (FK → staff, SET NULL). Alt text belongs to the media object, not
  the block: edit once, every block showing the image updates.
- Uploads: staff only, ≤ 10 MB, type sniffed from **magic bytes** (JPEG/PNG/GIF/WebP/AVIF); the client's
  Content-Type and extension are ignored. **No SVG** (script risk on the app origin).
- Blocks reference media via their `mediaId` attribute (image and card; one image per block). `PageDetail`/`PublicPage` include `media[]` (id, url, alt) for every referenced id, so
  a page renders without extra requests. Deleting media removes blob + row; blocks still pointing at it
  render nothing (editor shows "deleted" placeholder). No usage tracking yet.

## Networking (compose)
- Two networks: `backend` (`internal: true`: no egress, no other containers) holds postgres + seaweedfs; the api joins
  `backend` and `default` (renderer, frontend, SMTP egress). The renderer only receives POSTs from the api.
- Only frontend (`FRONTEND_PORT`) and api (`API_PORT`, for `pnpm dev`'s Vite proxy only) publish ports, both on
  **127.0.0.1**: Docker-published ports bypass host firewalls like ufw, so `0.0.0.0` would expose them.
- Production: a reverse proxy on the host (TLS) → `127.0.0.1:FRONTEND_PORT`. Its connections reach nginx from the
  Docker gateway, so `nginx.conf` trusts X-Forwarded-For from `172.16.0.0/12` (`real_ip_recursive`: the rightmost
  untrusted entry = the visitor) and passes only that IP on; the API's `ForwardedHeaders:KnownNetworks` trusts nginx.
  Without this every visitor would share the gateway IP and one rate-limit bucket. X-Forwarded-Proto from the proxy
  is passed through. The host proxy must set X-Forwarded-For (Caddy does by default; nginx: `$proxy_add_x_forwarded_for`).
- Hardening: every service has `no-new-privileges` and `cap_drop: [ALL]`; all processes run as non-root users (api
  1654, renderer node, frontend nginx 101, postgres 70, seaweedfs 1000). Only the postgres/seaweedfs entrypoints run
  as root briefly (chown their data dir, then drop) and keep CHOWN/DAC_OVERRIDE/FOWNER/SETUID/SETGID; backups-init keeps CHOWN.

## Backups: one archive = pg_dump + every media blob
Super admins (Profile → Backups) back up on demand or on a daily/weekly/biweekly/monthly schedule, and restore; staff can also export
single pages as JSON (`lib/pageExport.ts`, `POST /api/pages/import`, always a new draft). Code: `api-backend/Services/Backup/`.
- **Archive** `/backups/<auto|manual|pre-restore|upload>-yyyyMMdd-HHmmss.tar.gz`: `manifest.json` first (format,
  version, CMS version, newest EF migration, media count; the list reads only this entry), `db.dump` (`pg_dump -Fc`),
  `media/<storageKey>` per blob, `missing-media.json` when blobs were gone. The folder is the source of truth (no table).
- **Where**: fixed container path (`Backup:Directory`); the instance's compose file decides the host side
  (`BACKUP_DIR` bind mount, default `./backups`; the one-shot `backups-init` service chowns it to the api's uid 1654,
  as Docker creates a missing folder as root). The app can't change its own mount, so the admin only shows it.
- **Why the API runs pg_dump** (postgresql-client-18 in the image) instead of a DB-level or volume snapshot: works the
  same against a managed Postgres and any S3 store; blobs go through `IBlobStorage`, never SeaweedFS internals.
- **Consistent backup**: a REPEATABLE READ transaction exports its snapshot (`pg_export_snapshot()`), reads the media
  keys and runs `pg_dump --snapshot=…` while open, so dump and key list are the same instant. A blob deleted before it's
  copied is listed in `missing-media.json` (no lock on media deletes). Written to `.partial`, renamed when complete.
- **Left out** (`BackupTables.DataExcluded`): rows of `backup_settings` (the schedule survives a restore),
  `refresh_tokens`/`verification_tokens` (everyone signs in again) and `rendered_pages` (re-rendered).
- **Restore**, under one lock (backup/restore/upload/delete never overlap, busy = 409): read the whole archive (damage
  shows before anything changes) → refuse a newer schema (manifest migration unknown to this build) → `pre-restore`
  backup (the undo) → maintenance gate (every other `/api` request 503) → upload the archive's blobs (GUID keys:
  additive, harmless if the next step fails) → `DROP SCHEMA public CASCADE; CREATE SCHEMA public;` + `pg_restore -f -`
  piped into **one** `psql --single-transaction` (all or nothing; psql's input closes only after pg_restore succeeded,
  since end of input commits) → clear Npgsql pools → migrate (older backups catch up) → write the schedule back →
  delete blobs no restored media row references → re-render everything.
  Not `pg_restore --clean`: it drops only what the dump has, so tables of newer migrations would survive and break the
  migrate. The DB user must own the `public` schema (the compose superuser does; managed DBs: the database owner).
- **Schedule** (`BackupSchedule`, UTC): daily (time), weekly (weekday), biweekly (every 14 days from the first
  matching weekday after saving: the save is the anchor), monthly (day 1-28, so every month has it). A slot counts once
  it passed after the last run *and* after the settings were saved (turning it on at 10:00 with a 03:00 slot waits for
  tomorrow); downtime catches up with one run. Retention deletes only `auto-*` archives. `BackupScheduler` checks every
  minute; it also deletes **every** archive stored more than 2 years ago (`MaxAgeYears`, by the time in the file name,
  so an uploaded old backup counts from its upload).

## Site config: fields + groups shaped by the instance config
Sites want different info (firm, address, hours, VAT ID, socials...), and the site's *code* (footer, templates)
needs to find it. So the developer defines the shape in `cms.config.json` and the owner fills it in ("Configuration"):
- **Fixed fields**: one value each, e.g. the core `firmName` (page titles use it) or an instance's `vatId`.
- **Groups**: ordered key/value entries the owner adds, removes and reorders: the core `contact` group (address,
  phone, email presets; custom entries allowed) and instance groups such as `socials` (presets instagram, linkedin...).
  Presets fix key + type; custom entries (if `allowCustom`) carry the owner's own name and type; `required` presets
  can't be removed. Keys are unique per group (a second phone = custom entry "Mobile"), so lookups stay unambiguous.
- Instance code reads two typed maps from the API contract: `config.fields.firmName`, `config.groups.socials`
  (entries `{ id, key, label, type, value, address }`); no per-instance codegen. Helpers in `@trainpaths/cms/site`
  (`firmName`, `configEntry`, `filledEntries`, `addressLines`, `entryHref`, `entryText`) and the `SiteConfigEntry`
  component (one entry's value, as the footer shows it).
- Storage: one jsonb `Values` column of a singleton row (`site_config`); reads fit it to the current schema (the
  developer may change it after values were saved), writes are validated against it.
- `type` (`text`|`email`|`phone`|`link`|`address`) is a rendering hint: input type, mailto/tel/link, address lines.
  Link values must be absolute http(s) (rendered as `href`). The default footer shows logo, firm name and contact;
  other groups appear where the developer places them (SiteFooter override, templates).
- Only what the system itself consumes gets a dedicated column: `LogoMediaId` (public footer, later the public
  nav) and `IconMediaId` (public favicon), both FKs to `media_assets` with ON DELETE SET NULL. Both are picked
  from the media library, so raster only (no SVG, same reason as media).
- Two icons: the admin/CMS icon is the package's `public/favicon.svg`; public pages swap in the site icon.

## Menus: jsonb tree with page ids, resolved on read
A menu (`menus`) is one row with its item tree in a `jsonb` column (same value-converter pattern as blocks): it is
always edited and saved as a whole, and the public site reads it as a whole. Items reference pages by **id**, not slug,
so renaming a slug or title never breaks a menu; `GET /api/public/menus/{handle}` resolves ids to the current slug/title
and leaves out drafts and deleted pages (no FK from jsonb, so deletes are handled at read time). In the editor an
item is just label + optional link: typing a page's path (`/about`) stores the page id; anything else is a URL, either
absolute http(s) or a same-site path (rendered as `href`/RouterLink; `//host` and other schemes rejected). A menu's only identifier is its **handle** (kebab-case, normalized from
what the owner types, immutable after creation because site code loads menus by it); there's no separate display name,
it would only duplicate the handle. `main` is seeded and undeletable because the public header (`SiteHeader.vue`)
renders it. Nesting ≤10, ≤200 items.

## Tags: normalized tables
Tags are shared across pages and need counts ("most popular") and lookups, so they're rows (`tags`) with a
`page_tags` junction rather than a jsonb array on the page. Tags are single words (no spaces); the name is normalized before
it's stored (trim, lowercase) so one unique column suffices. Tags without pages are deleted on every tag write/page delete,
so autocomplete only offers tags in use. Setting tags doesn't bump `UpdatedAt` (the pages list sorts by it).

## Block contract (frontend ⇄ API ⇄ DB)
```json
{ "id": "nanoid", "name": "card", "attributes": { "title": "Hi", "level": 2, "open": true }, "innerBlocks": [] }
```
- The backend does **not** know block types. It validates structure only (`PageService.ValidateBlocks`):
  non-empty `id` (≤64), `name` matches `^[a-z0-9-]+$`, attribute values are string/number/boolean,
  depth ≤ 10, ≤ 2000 blocks. Explicit JSON nulls for `attributes`/`innerBlocks` are normalized to empty.
- Adding a block type is therefore frontend-only (see `BLOCK_SYSTEM.md`).

## Auth model
- Editor endpoints: `[Authorize(Policy = StaffOnly)]`. Router guard: `meta.requiresStaff` checks
  `authStore.authType === 'staff'`. Staff = site owners.
- Customer accounts only with `publicAuth` in the instance config (default off): otherwise the customer auth API
  answers 404 (`PublicAuthGateAttribute`), the admin router sends `meta.publicAuth` routes to not-found and the main
  menu seed has no Login link. Reserved slugs stay reserved either way.
- Public reads: `GET /api/public/pages/{slug}` is anonymous and only returns **published** pages (404 otherwise).

## Slugs and the public route
- Public URL is `/{slug}` (clean, single segment). vue-router ranks static routes above `/:slug`, so app
  routes always win; the API additionally rejects `PageService.ReservedSlugs`
  (`login`, `register`, `profile`, `change-password`, `forgot-password`, `reset-password`, `verify-email`,
  `dashboard`, `admin`, `api`, `health`, `assets`, `_diagnostics`). **Adding a top-level frontend route?
  Add its first segment to `ReservedSlugs`.**
- Slug rules: kebab-case, 1–100 chars. Omitted on create → generated from the title, de-duplicated with
  `-2`, `-3`…; a reserved generated slug gets `-page`. An explicitly taken slug → 409.
- Slug changes are saved separately from content (`PUT` with only `slug`) so a bad slug never blocks autosave.
- `ReservedSlugs` also routes requests: nginx sends every single-segment path to the API's HTML endpoint, which
  answers reserved ones with the admin SPA (see Public pages). A frontend route missing from the list gets a 404.

## Public pages: rendered when content changes (Vue SSR, no Nuxt)
Why: an SPA shows nothing until JS boots and the page is fetched (~2–3 extra round trips, LCP ~2–3.5 s on
mid-range mobile vs ~1–1.5 s with HTML), and link-preview scrapers (Slack, WhatsApp, LinkedIn...) run no JS, so
every shared link looked the same. Nuxt was rejected (second framework/server for the whole app); C# rendering
(Razor) was rejected because every block would need a second view.
```
staff save ─► API IRenderQueue ─► RenderWorker ─POST /render─► renderer (Node, frontend/server/render-server.js)
                                       └─► rendered_pages (Postgres) ◄─ PublicHtmlController ◄─ nginx ◄─ visitor
```
- The public site is a separate small Vue app (`frontend/src/public/`, template = the instance's `public.html`) built from the same
  view components as the editor preview, rendered with `vue/server-renderer` and **hydrated** in the browser
  (client navigation between public pages fetches `/api/public/pages/{slug}`). It ships no editor code.
- Render triggers (controllers call `IRenderQueue`): content edit of a published page → that page; publish,
  unpublish, delete, menu, site config, media edit/delete → all pages. A one-page job widens to all when the
  page's title/slug differs from what it was rendered with (menus and footer show those). Debounced 1 s.
- **Freshness:** a change marks HTML rendered before it stale right away (`RenderStatus`); until re-rendered the
  API serves the client-rendered shell (`/public.html`, fetches live data). So edits are visible immediately and
  rendered HTML never shows old content. In-memory state: assumes **one API instance**.
- **Deploys:** stored HTML names hashed asset files of its build. The renderer reports a version (hash of
  template + SSR bundle); only HTML of the current version is served, and a version change re-renders everything
  (periodic check, 60 s; also on API start). The not-found page lives in memory (re-rendered with everything).
- **Routing:** nginx proxies `^/[a-z0-9-]*$` (incl. `/`) to `GET /api/public/html/{slug}`. Responses: stored HTML
  (ETag, `no-cache`), 404 + rendered not-found page, 301 `/home` → `/`, or `X-Accel-Redirect` to `/index.html`
  (reserved slug → admin SPA) / `/public.html` (no fresh HTML). Admin-SPA links to `home`/`public-page` are full
  page loads.
- No renderer configured (`Renderer:BaseUrl` empty, e.g. tests) → everything falls back to the shell.
- **Site meta** (`src/public/head.ts`): document title "{page meta title} - {firm name}", the firm name alone without
  a meta title (no firm name: meta title, then page title); description = page meta description, else the first
  paragraph; og:site_name = firm name; og:image = first image, else the site's share image; `<html lang>` = instance
  `site.lang`, written into index.html + public.html at build time by `cms()` (a static value: no runtime plumbing).
- Dev: `pnpm dev` server-renders public pages itself (`frontend/server/dev-ssr.js`, Vite `ssrLoadModule`), so
  hydration mismatches show up in development; the renderer container isn't needed.

## Limits
- Global request body cap is 64 KB (Kestrel). Page writes raise it to **2 MB** via
  `[RequestSizeLimit]`; nginx has a matching `location /api/pages { client_max_body_size 2m; }`.
- Media uploads: 10 MB (`MediaController` `[RequestSizeLimit]` + form limit); nginx `location = /api/media` allows 11m.
- Rate limiting: page and media endpoints use policy `pages` (600/min per IP) — the editor autosaves every
  4 changes, which the `auth` policy (120/min) would throttle.

## Editor state & saving
- Pinia store `useEditorStore` (`frontend/src/stores/editor.ts`) holds the page's `blocks` tree.
- Undo/redo: snapshot history (max 50); every mutation pushes a snapshot.
- Autosave after 4 changes; saves are **serialized** (a save requested mid-flight re-runs after) so an
  older snapshot can't overwrite a newer one. Leaving the editor flushes pending changes. Publish saves first.
- `PUT /api/pages/{id}` is a partial update — omitted fields are untouched.

## Styling
- Tailwind v4, tokens in `@theme` in `frontend/src/style.css`; spacing unit = 1px (`p-16` = 16px).
- The web-editor look is the app-wide theme (primary `#1a73e8`, gray borders, white surfaces, system font).
- `style.css` restores three Tailwind v3 defaults the ported editor relies on (gray-200 border colour,
  gray-400 placeholders, pointer cursor on buttons).
- Exceptions to the "Tailwind only" rule: `@apply` component classes in `lib/web-editor/web-editor.css` and
  SFC `<style>` blocks (with `@reference`), and inline `:style` for **user-chosen block colours** (arbitrary
  hex values can't be Tailwind classes).

## Why not Nuxt anymore
The CMS already has a Vue SPA + .NET API; Nuxt's server routes were the editor's only backend. Porting
meant: explicit imports instead of auto-imports, `RouterLink` for `NuxtLink`, the hey-api client instead of
`$fetch`, and `registerAllBlocks()` (now called by the editor itself) instead of a Nuxt plugin. Public pages
later got server rendering without Nuxt (see Public pages).
