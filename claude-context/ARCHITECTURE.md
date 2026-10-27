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
  The API is its only runtime reader (no frontend/backend default drift; `site.lang` and `theme.colors` are the keys
  the `cms()` Vite plugin applies at build time); the admin reads `/api/public/instance`. Pages it
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
  `Blocks` (**jsonb**, the whole tree), `CreatedAt`, `UpdatedAt`, `PublishedAt`, `CreatedById`/`UpdatedById` (FK → staff, SET NULL),
  plus the published snapshot (see Draft vs live).
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
it's stored (trim, lowercase) so one unique column suffices. Tags without pages are deleted (one `DELETE`) after a tag write/page delete that removed one,
so autocomplete only offers tags in use. Setting tags doesn't bump `UpdatedAt` (the pages list sorts by it).

## Block contract (frontend ⇄ API ⇄ DB)
```json
{ "id": "nanoid", "name": "card", "attributes": { "title": "Hi", "level": 2, "open": true },
  "translations": { "de": { "title": "Hallo" } }, "innerBlocks": [] }
```
- `translations` (optional, omitted when empty): per language code (`^[a-z]{2,3}$`), primitive values like
  attributes. Empty languages are dropped on write; codes need not be configured (removing a language from the config
  and adding it back keeps its text). Public reads never contain it (`PageService.Localize`).
- The backend does **not** know block types. It validates structure only (`PageService.ValidateBlocks`):
  non-empty `id` (≤64), `name` matches `^[a-z0-9-]+$`, attribute values are string/number/boolean,
  depth ≤ 10, ≤ 2000 blocks. Explicit JSON nulls for `attributes`/`innerBlocks` are normalized to empty.
- Adding a block type is therefore frontend-only (see `BLOCK_SYSTEM.md`).

## Draft vs live (save ≠ publish)
Owners must be able to keep working on a published page without exposing half-done edits, so a page has two copies
of its **content** (title, meta title, meta description, blocks, title/meta translations): the **working copy**
(`Title`, `MetaTitle`, `MetaDescription`, `Blocks`, `Translations`) that every save writes, and the **published
snapshot** (`PublishedTitle`, `PublishedMetaTitle`, `PublishedMetaDescription`, `PublishedBlocks`,
`PublishedTranslations`; null title/blocks = never published).
- Slug and tags have no snapshot: routing/organisation, live right away (a slug change on a published page re-renders).
- Save (`PUT` with content) never touches the snapshot. Publish (`status: published`, also on an already published page
  = "publish changes") copies working copy → snapshot, sets `PublishedAt`, re-renders. Unpublish (`status: draft`) only
  flips the status: the snapshot stays (cheap, allows a later diff), a republish copies the then-saved working copy.
- Everything public reads the snapshot: `PageService.Live` (public page/state endpoints, renderer input), menu labels
  and footer links (`PublishedTitle`), the render worker's title check. The editor and the admin preview read the working copy.
- `HasUnpublishedChanges` (stored bool, in `PageSummary`/`PageDetail`): published and the working copy differs from the
  snapshot (block trees compared as JSON, translations per language); recomputed on every content/status write, so
  lists don't compare trees. A translation-only edit counts.
  Saving the live content back clears it.
- Page states in the editor: **Unsaved** (client-side dirty), **Draft**, **Published**, **Published · unpublished
  changes**. Publishing always publishes the *saved* state: the controls are disabled while there are unsaved edits.
- Seeded pages start published with snapshot = working copy (no pending changes).

## Auth model
- Two apps, split by audience: the **admin SPA** (`/admin*`) is staff-only (site owners); everything the website's
  visitors use, customer accounts included, is the **public site** (server-rendered, see Public pages → Customer
  screens). Why: the visitor's login and account pages should load as fast as the site, in its layout and theme, and
  a customer has no business in the admin chrome.
- Editor endpoints: `[Authorize(Policy = StaffOnly)]`. Admin router: every `/admin*` route has `meta.requiresStaff`
  (`authStore.authType === 'staff'`); a customer session there → full page load to `/`.
- Customer accounts only with `publicAuth` in the instance config (default off): otherwise the customer auth API
  answers 404 (`PublicAuthGateAttribute`), the public site has no screens (`/login` is an unknown page) and the main
  menu seed has no Login link. Reserved slugs stay reserved either way.
- Public reads: `GET /api/public/pages/{slug}` is anonymous and only returns **published** pages (404 otherwise), as
  their published snapshot.

## Slugs and the public route
- Public URL is `/{slug}` (clean, single segment). vue-router ranks static routes above `/:slug`, so app
  routes always win; the API additionally rejects `PageService.ReservedSlugs`
  (customer screens `login`, `register`, `forgot-password`, `reset-password`, `verify-email`, `user` (account pages
  `/user/*`), then `dashboard`, `admin`, `api`, `health`, `assets`, `_diagnostics`). **Adding a top-level frontend
  route? Add its first segment to `ReservedSlugs`.**
- Slug rules: kebab-case, 1–100 chars. Omitted on create → generated from the title, de-duplicated with
  `-2`, `-3`…; a reserved generated slug gets `-page`. An explicitly taken slug → 409.
- Slug changes are saved separately from content (`PUT` with only `slug`) so a bad slug never blocks autosave.
- `ReservedSlugs` also routes requests: nginx sends every single-segment path to the API's HTML endpoint, which
  answers customer screens itself and the other reserved ones with the admin SPA (see Public pages). A frontend route
  missing from the list gets a 404.
- On a localized site the language codes are reserved too (`PageService.SlugError`: `/de` is the German home); a
  title that slugifies to one gets `-page`. A page that already had a newly configured code as slug is unreachable:
  logged as a warning on startup, never renamed automatically.

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
- Render triggers (controllers call `IRenderQueue`): saves of the working copy render nothing; publishing changes or a
  slug change of a published page → `Page(id)`, plus `Refresh()` when the live title/slug changed (menus and footer show
  those). Publish/unpublish (status change) → `Page(id)` + `Refresh()`, delete → `Refresh()`. The
  refresh marker is always set at save time: a page loaded before the worker picks the job up would otherwise show the
  old menu without the client refresh. (The worker still widens a one-page job to all when the page left/joined the
  public site or its title/slug differs from what it was rendered with: safety net.) Main menu,
  site config, media alt edit/delete → `Refresh()` (all pages, quietly); other menus, a media rename and uploads render nothing (not in
  stored HTML). Backup restore → `All()`. Debounced 1 s.
- **Freshness** (`RenderStatus`, in memory: assumes **one API instance**):
  - Own content changed (`Page(id)`): that page's stored HTML is stale right away; until re-rendered the API serves the
    client-rendered shell (`/public.html`, fetches live data), so the edit is visible immediately.
  - Something around the body changed (menu, footer, head tags, publish state of another page, media): stored HTML
    keeps being served while a **quiet re-render** runs. HTML rendered before the change gets `data-refresh` on its
    `#__STATE__` (string replace on the response, ETag suffix `-r`), and the public client then refetches the main menu
    and site config after hydration (one `GET /api/public/state`, `public/refresh.ts`), so header/footer show the
    change right away. Unmarked pages
    (the normal case) cause no extra requests. Crawlers see the old menu/head tags for a few seconds; a click on a link
    to a just unpublished/renamed page can 404 in that window; a deleted image is broken until re-rendered.
    Splitting the header into its own stored fragment was rejected (separate Vue app, active link after mount, split
    state, breaking `public.html` change for instances).
  - `All()` (restore only) marks everything stale: `rendered_pages` isn't in backups, but a render in flight during
    the restore could store pre-restore HTML afterwards.
- **Public cache** (`PublicCache`, in memory, same one-instance assumption): resolved public menus by handle + the
  site config, per language; a miss loads every language in one go (same queries, only page titles differ), read by the public menu/site-config endpoints and every render batch. `Refresh()`/`All()` invalidate it
  (they already fire on every write that changes the main menu, the site config, a page title/slug, publish state or
  media), `MenusController` on any menu write (other menus don't go through `Refresh()`). Entries carry a generation, so
  a load racing an invalidation is never served. Stored HTML/page JSON is deliberately **not** cached: the stored-HTML
  lookup is one indexed query (page + current `rendered_pages` row joined).
- **Batched writes:** the worker processes pages in chunks of 20: pages + media refs in two queries, existing row keys in
  one (rows are updated without reading the old HTML), one `POST /render` per page and language (Node renders one at a time; a batch endpoint would hit the 4 MB body limit), one
  commit per chunk (a page is fresh once its chunk commits). An FK violation (page deleted mid-render) falls back to
  row-by-row for that chunk. The log line `Rendered {n} public page(s) in {k} language(s) in {ms} ms` shows what a full render costs.
- **Languages:** `rendered_pages` is keyed `(PageId, Lang)` (code); every page, the not-found page and the screens
  render once per configured language (`RenderState.lang`), all languages of a page go stale together (freshness and
  the refresh marker are per page). The widen check compares the default language's row only. A full render deletes
  rows of languages no longer configured; a language added to the config counts as missing HTML (version check: current
  rows in configured languages < published pages × languages, two counts).
- **Deploys:** stored HTML names hashed asset files of its build. The renderer reports a version (hash of
  template + SSR bundle); only HTML of the current version is served, and a version change re-renders everything
  (periodic check, 60 s; also on API start). The not-found page lives in memory (re-rendered with everything).
- **Customer screens** (with `publicAuth`): `/login`, `/register`, `/forgot-password`, `/reset-password`,
  `/verify-email` and the account pages `/user/profile`, `/user/password` are routes of the public app
  (`src/public/screens/`, lazy chunks), rendered like the not-found page: in memory (`RenderStatus.Screens`), once per
  full render with the current menu/config, so a menu change re-renders them; they get the refresh marker like stored
  pages and are served only while their renderer version is current. They show the same HTML to everyone: the redirect
  query, a reset token and the session are read after hydration; account pages render a shell and load the user once
  `ensureSession()` confirms a customer (else → `/login?redirect=…`). Login/register navigate back client-side to the
  page the visitor came from (the router adds `?redirect=` when leaving a page for them; `safeRedirect` allows
  same-site paths only, never a guest screen or `/admin`). The header's `/login` menu item reads "Account" for a
  signed-in customer (client-only, from the `auth_type` hint and the auth store).
  `publicAuth` is checked on the server only: the worker renders screens and `PublicHtmlController` serves them only
  with it on; off, a screen path gets the not-found page. The public app always has the screen routes and finds out
  from the embedded state: on the first navigation a screen route whose `slug` isn't that screen's (it's `not-found`)
  becomes the catch-all route under the same URL, so it hydrates as not-found. (Left open: with public auth off, a
  client-side link to `/login` the owner adds to a menu shows a form whose API answers 404.) One list of screen slugs in three
  places, kept in sync: `src/public/screens/index.ts` (Vitest checks it against `server/screens.js`, read by dev SSR)
  and the API's `PublicScreens`.
- **Routing:** nginx proxies `^/(?!api/)([a-z]{2,3}/)?(user/)?[a-z0-9-]*$` (incl. `/`) to `GET /api/public/html/…`
  (`{slug}`, `user/{name}`, `{lang}/{slug}`, `{lang}/user/{name}`).
  Responses: stored HTML (ETag, `no-cache`), a customer screen (`no-cache`), 404 + rendered not-found page (unknown
  slug, unknown `user/*`, screens with public auth off), 301 `/home` → `/`, or `X-Accel-Redirect` to `/index.html`
  (other reserved slug → admin SPA) / `/public.html` (no fresh HTML). Admin-SPA links to `home`/`public-page` are full
  page loads; so are public-app links into `/admin`.
- **Language routing** (localized site only): `/de` = German home, `/de/about`, `/de/user/profile`; the default
  language's prefix 301s to the unprefixed path (`/en/about` → `/about`), an unknown prefix is a 404, `/de/admin` 301s
  to `/admin`, `/de/home` to `/de`. An unprefixed page/screen/home path with a `cms_lang` cookie naming another
  configured language 302s to that language's path (query kept; admin routes never redirect). Every response carries
  `Vary: Cookie`. The public app's router history base is the prefix (`/de/`), so every router link (menus, footer,
  screens) gets it for free; `src/public/locale.ts` (`localePath`, `stripLocale`) covers the code that builds URLs
  itself (head tags, the shell's fetch, the language switch). The public JSON endpoints take `?lang=`.
- No renderer configured (`Renderer:BaseUrl` empty, e.g. tests) → everything falls back to the shell.
- **Site meta** (`src/public/head.ts`): document title "{page meta title} - {firm name}", the firm name alone without
  a meta title (no firm name: meta title, then page title); description = page meta description, else the first
  paragraph; og:site_name = firm name; og:image = first image, else the site's share image; `<html lang>` = the
  render's language tag (the template carries the default's from `cms()`; `splice` replaces it per render); on a
  localized site canonical = the language's URL, `hreflang` alternates for every language + `x-default` (unprefixed),
  `og:locale` + `og:locale:alternate`.
- Dev: `pnpm dev` server-renders public pages and customer screens itself (`frontend/server/dev-ssr.js`, Vite `ssrLoadModule`), so
  hydration mismatches show up in development; the renderer container isn't needed.

## Localization (issue #45)
Sites need several languages (Swiss clients: de, fr, it + en), in the admin and on the public site. Work plan for the
remaining phases (one PR each): `claude-context/localization/` (README first).
- **Config**: `site.lang` = a tag (one language) or `{ default, localization: [tags] }`. A language's **code** = its
  primary subtag, unique per site: URL prefix, `cms_lang` cookie, message file, key of stored translations (survives
  `de-CH` → `de-DE`). The **tag** goes into `<html lang>`, `hreflang` and `Intl` formats. Validated by the API
  (`CmsConfigLoader`: tags, unique codes, codes not a reserved/configured slug) and `cms()` (`server/site-lang.js`).
- **Languages reach the frontend at build time** (`__CMS_LANGUAGES__`, `src/i18n/languages.ts`) like the palette: the
  public SSR needs them without a request, and `<html lang>` was build-time already. The API still reads the same file
  (routing/rendering), so both read one source.
- **UI strings**: vue-i18n (Composition API), messages per code in `src/locales/public/` (eager: both apps, the server
  render can't wait for a chunk) and `src/locales/admin/` (lazy chunk per language), English = source + fallback;
  the instance's `src/locales/<code>.json` is deep-merged over both. One i18n instance per app (per server render).
  Names that come from config in English (block titles, site config labels) are translated by key unless the config
  renamed them (`i18n/siteConfigLabels.ts`). Pinia stores translate through `translate()`, registered by `createAdmin()`.
- **Admin language** = the `cms_lang` cookie (written by `LanguageDropdown`, not HttpOnly) else the default; switching
  swaps the strings in place. One cookie for admin and public site ("same setting"): no DB column, no API call.
- **Public site** (Phase 2): language prefix with the code, default unprefixed (`/about`, `/de/about`), one
  stored HTML per page and language (`rendered_pages` key `(PageId, Lang)`), `hreflang` alternates; an unprefixed URL
  with a `cms_lang` cookie for another language redirects (see Public pages → Language routing). The public
  `LanguageDropdown` (default footer) remembers the choice and loads the same page under the other language's URL (full
  page load: stored HTML + head of that language). Rejected: same URL by cookie/`Accept-Language` (crawlers and
  link previews only ever see one language) and client-side text swapping (wrong language flashes, no SEO, hydration
  mismatches). Storage: a few KB per page and language; a full re-render takes N× longer.
- **Content** (Phase 3): per-field translations on one shared block tree (`translations: { code: { attr } }` per
  block, opt-in `translatable` attributes, page title/meta per language in `pages.Translations` jsonb, published with
  the rest), resolved by the API for public reads (`PageService.Localize`: overlay of non-empty values, else the default;
  `translations` stripped). Layout, images, colours and link URLs stay shared (a link block's site path follows the
  page's language: `localeHref`, like router links); add/move/delete of blocks affects every language.
  Menus and footer links show the translated live title (`PageService.LiveTitle`; `PublicCache` keyed per language; a
  translated title change re-renders other pages like a rename). Editor: `store.editLang` (header select + Page tab
  "Translations" with untranslated counts); `useBlockAttribute` reads/writes the edited language for translatable
  attributes, `usePageText` for title/meta; the admin preview localizes the working copy client-side
  (`core/localize.ts`, mirror of `Localize`). Typed page title/meta translations are not in the undo history (like the title); a DeepL fill's are (its undo step
  restores them). Saves drop block translations of attributes that aren't `translatable` (`pruneTranslations`): the API
  overlays any stored key publicly, so a stale one would show there while the editor can't.
  Rejected: a block tree per language (layouts drift apart, every image change done N times).
- **Machine translation** (Phase 4, optional): with `DEEPL_API_KEY` set, the editor offers "Translate missing fields"
  (and "Re-translate all", behind a confirm) for the edited language: `core/translatable.ts` `textFields` collects the
  default-language text of every untranslated translatable field (+ title/meta), one `POST /api/translate` (staff,
  ≤500 texts, ≤10 000 chars each, source = default language), results land in the working copy as one change and one
  undo step (`applyTranslations`). Nothing is saved or published by itself: the owner reviews, saves, publishes.
  `ITranslator` seam → `DeepLTranslator` (free keys `:fx` → api-free.deepl.com; batches of ≤ 50 texts and ≤ 100 KB, 3 in parallel; `EN-US`/`EN-GB`,
  `PT-PT`/`PT-BR` targets; no Swiss German at DeepL → `ß` replaced by `ss` for `de-CH`; empty texts never sent).
  **Privacy**: page text leaves the server to DeepL. The Pro API doesn't keep texts; the free API may use them to
  improve its models. Site owners must know before a key is set. Cost = characters translated (only missing fields by
  default).
- nb-ui has no message hook: its built-in texts are English (`UnsavedChangesDialog` → our `components/UnsavedDialog.vue`;
  aria-labels like "Close" stay English) and its `Popover` touches `document` in setup, so `LanguageDropdown` server-renders
  only its trigger.

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
  older snapshot can't overwrite a newer one. Leaving the editor flushes pending changes. Saves only write the working
  copy (Draft vs live). Dirty tracking: `isDirty` = the content (title, meta, translations, blocks) differs from the last saved JSON
  (undo back to it = clean); Save is disabled when clean and `savePage` skips clean pages. `publish()`/`unpublish()`
  send `{status}` alone, after any save in flight; the rules for the buttons (header + Page tab) live in one composable
  (`lib/web-editor/composables/usePublishState.ts`).
- `PUT /api/pages/{id}` is the only page write besides create/import/duplicate/delete: a partial update of title, slug,
  meta, blocks, `status` and `tags` (omitted fields untouched), validated as a whole before anything changes, one
  transaction. It answers with the slim `PageSummary` (no 2 MB blocks echo); the pages store merges it plus what was
  sent into its cached detail (`applySaved`). Tags alone don't bump `UpdatedAt` and render nothing.
- The shell (no stored HTML yet) loads page + main menu + site config in one `GET /api/public/state/{slug}`; dev SSR
  uses the same call.

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
