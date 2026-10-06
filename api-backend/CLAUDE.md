# api-backend

.NET 10 ASP.NET Core + EF Core 10 + Npgsql + JWT Bearer + Swashbuckle.
Feature areas: **auth** (from the template), **pages** (the CMS block editor's storage, + tags), **media**
(uploaded images in S3-compatible blob storage), **site config** (fixed fields + groups shaped by the instance config, logo, icon),
**menus** (navigation trees), **backups** (DB + media archives) and the **instance config** (`cms.config.json` of the client site).
Design decisions behind pages: `claude-context/ARCHITECTURE.md` (repo root).

## Instance config (`Services/Cms/`)
The client site's developer config, baked into its API image (`/app/cms.config.json`; the playground mounts it in
docker-compose). The API is its **only runtime reader**: the admin gets what it needs from `/api/public/instance`. Exception:
`site.lang` is applied at frontend build time by the `cms()` Vite plugin (the API still validates it).
```
CmsConfig.cs        records CmsConfig { publicAuth, site.lang, blocks.exclude, pages[], siteConfig } + CmsPageConfig { slug, title, template?,
                    locked, editable, footer, blocks }; IsLocked = locked || template; SiteConfigSchema() (core + instance)
CmsConfigLoader.cs  Load (path = Cms:ConfigPath relative to the content root, default cms.config.json; missing →
                    CmsConfig.Default = DefaultPages + public auth off), Parse (strict JSON: unknown keys fail,
                    `$schema`/comments/trailing commas ok), Validate (slugs via PageService.ValidateSlug, unique, title,
                    kebab template, editable:false needs a template, blocks via ValidateBlocks, exclude names, lang tag,
                    site config keys camelCase + unique, defaults/required ⊆ presets) → InvalidOperationException listing problems
SiteConfigSchema.cs SiteConfigSchema + ConfigFieldDef/ConfigGroupDef/ConfigPresetDef + CoreSiteConfig (firmName, contact): see Site config
DefaultPages.cs     the default config's pages (see Pages)
PublicAuthGateAttribute.cs  resource filter on CustomerAuthController: 404 unless PublicAuth (endpoints stay in OpenAPI;
                    [Authorize] middleware runs first, so protected ones answer 401 to anonymous callers)
PublicInstanceController    GET /api/public/instance → InstanceConfig(publicAuth, excludedBlocks, siteConfig schema)
```
- Singleton, built lazily (test overrides apply) and resolved right after `Build()`: an invalid file stops startup.
- **Page flags are derived by slug** (`cms.Page(slug)`), never stored: no migration, removing a page from the config
  unlocks it. Keep `frontend/cms.config.schema.json` in sync with the records.

## Pages (CMS)
Staff (= site owners) manage pages; anyone reads published ones. The block tree is one `jsonb` column.
```
PagesController        /api/pages            [Authorize(StaffOnly)], rate limit "pages"
  GET    /                     → PageSummary[] (incl. blockCount, newest updated first)
  GET    /{id}                 → PageDetail | 404
  POST   /                     CreatePageRequest(title, slug?, blocks?) → 201 PageDetail | 400 | 409 slug taken
  POST   /import               ImportPageRequest(title, slug?, blocks, metaTitle?, metaDescription?, tags?) → 201
                                 ImportPageResult(page, missingMediaIds) | 400 — always a new draft, never overwrites
  PUT    /{id}                 UpdatePageRequest(title?, slug?, blocks?) — partial → PageDetail | 400 (incl. locked slug,
                                 non-editable blocks) | 404 | 409
  PUT    /{id}/tags            UpdatePageTagsRequest(tags[]) — full replace → PageDetail | 400 | 404 (UpdatedAt untouched)
  POST   /{id}/publish         → PageDetail (sets PublishedAt)
  POST   /{id}/unpublish       → PageDetail (clears PublishedAt)
  DELETE /{id}                 → 204 | 400 (locked config page) | 404
  POST/PUT carry [RequestSizeLimit(2 MB)] (global cap is 64 KB)
PublicPagesController  /api/public/pages     [AllowAnonymous], rate limit "pages"
  GET    /{slug}               → PublicPage (published only) | 404
TagsController         /api/tags             [Authorize(StaffOnly)], rate limit "pages"
  GET    /                     → TagUsage[] (name, pageCount; most used first)
```
- Tags: `Models/Pages/Tag.cs` (`tags`: one `Name` column, always stored normalized by `Tag.Normalize` = trim +
  lowercase; single words, whitespace inside → 400; unique) + `PageTag` junction (`page_tags`, cascade both ways). `TagLimits`: ≤32 chars,
  ≤20 per page. `PageService.SetTagsAsync` creates missing tags and deletes tags left without pages (also after a
  page delete), so `/api/tags` only lists tags in use. `PageSummary`/`PageDetail.Tags` sorted by name.
- `Models/Pages/Page.cs` (entity, `PageStatus` Draft|Published stored as string), `Models/Pages/Block.cs`
  (recursive: `Id`, `Name`, `Dictionary<string, JsonElement> Attributes`, `List<Block> InnerBlocks`).
- `Services/Pages/PageService.cs` returns `PageResult<T>` (`PageError`: NotFound | SlugTaken | Invalid);
  the controller maps errors to ProblemDetails. Status is exposed as lowercase `"draft"|"published"`.
- Slugs: kebab-case, ≤100, generated from title when omitted (dedup `-2`, `-3`), `ReservedSlugs` rejected
  (frontend top-level routes — keep in sync with `frontend/src/router/index.ts`; `PublicHtmlController` also uses
  the list to send those paths to the admin SPA).
- Block validation (`ValidateBlocks`): id non-empty ≤64, name `^[a-z0-9-]+$`, attributes primitive only,
  depth ≤10, ≤2000 blocks; null attributes/innerBlocks normalized to empty. No knowledge of block types.
- `AppDbContext` maps `Blocks` via a System.Text.Json value converter (camelCase) + ValueComparer, column type `jsonb`.
- `Services/Pages/PageSeeder.cs`: on startup (after `StaffBootstrapper`) seeds the config's pages, published: **all**
  while the pages table is empty (off with `Bootstrap:SeedPages=false`: the test `ApiFactory` does this,
  `PageSeederTests` turns it on), and **locked/template pages on every start** when their slug is missing.
  `Services/Cms/DefaultPages.cs` = the default config's pages: **Home** (`home`, served at `/`), **Privacy Policy**
  (`privacy-policy`), **Legal** (`legal`), the last two `footer`. Block attributes must match the frontend blocks'
  `attributes` (`blocks/*/index.ts`).
- Import (`PageService.ImportAsync`, page export files of `frontend/src/lib/pageExport.ts`): same validation as create +
  meta + tags (validated before anything is written); slug = the given one (or the title) through `UniqueSlugFromAsync`,
  so taken/reserved/invalid slugs get a free variant instead of 409. `missingMediaIds` = `mediaId`s
  (`MediaService.CollectIds`) not in `media_assets`: kept in the blocks, image renders empty.
- Page meta: `MetaTitle` (≤70) / `MetaDescription` (≤200), columns (default ''), edited in the editor (partial PUT; empty
  clears), served in `PageDetail` + `PublicPage`; the public `<head>` builds the title "{meta} - {firm name}" from them.
- Config pages (`CmsConfig`): **locked** (`locked` or `template`) → slug change / delete = 400; `editable: false` → block
  writes = 400 (title, status, tags still allowed). `PageSummary.Locked`, `PageDetail.Template/Locked/Editable`,
  `PublicPage.Template` (the instance template the public app renders it with).

## Media
Bytes in blob storage (SeaweedFS in docker-compose), metadata + alt text in `media_assets`. Why: `claude-context/ARCHITECTURE.md`.
```
MediaController        /api/media            [Authorize(StaffOnly)], rate limit "pages"
  GET    /                     → MediaItem[] (newest first)
  GET    /{id}                 → MediaItem | 404
  POST   /                     multipart file (+ alt) → 201 MediaItem | 400 (empty/too big/not an image)
  PUT    /{id}                 UpdateMediaRequest(alt, fileName?) → MediaItem | 400 | 404 (fileName = display name only, null keeps it)
  DELETE /{id}                 → 204 | 404 (deletes blob, then row)
  POST carries [RequestSizeLimit] + [RequestFormLimits] = 10 MB + 64 KB
PublicMediaController  /api/public/media     [AllowAnonymous], rate limit "pages"
  GET    /{key}                → file stream (immutable cache, CSP sandbox) | 404 (unknown or malformed key)
```
- `Services/Media/IBlobStorage.cs` (Put/Get/Delete, `BlobObject`) → `S3BlobStorage` (AWSSDK.S3, path-style,
  SDK v4 checksums set to WHEN_REQUIRED, bucket auto-created on first put). Singleton.
- `MediaService`: sniffs magic bytes (JPEG/PNG/GIF/WebP/AVIF, **no SVG**), key `{id:N}.{ext}`, buffers
  uploads (≤10 MB); a failed DB insert deletes the orphaned blob. `ResolveRefsAsync(db, blocks)` collects
  `mediaId` attributes → `MediaRef[]`; `PageService` puts it in `PageDetail.Media` /
  `PublicPage.Media`.
- `S3StorageOptions` (`Storage:S3`) is validated with `ValidateOnStart` (not eagerly in Program.cs, so
  WebApplicationFactory config overrides apply). Skipped during OpenAPI generation.

## Menus
```
MenusController        /api/menus            [Authorize(StaffOnly)], rate limit "pages"
  GET    /                     → MenuSummary[] (main first, then by handle)
  GET    /{id}                 → MenuDetail | 404
  POST   /                     CreateMenuRequest(handle) → 201 | 400 | 409 handle taken
  PUT    /{id}                 UpdateMenuRequest(items) — full replace, 512 KB → MenuDetail | 400 | 404
  DELETE /{id}                 → 204 | 400 (main) | 404
PublicMenusController  /api/public/menus     [AllowAnonymous], rate limit "pages"
  GET    /{handle}             → PublicMenu (resolved) | 404
```
- `Models/Menus/Menu.cs`: `menus` (unique kebab `Handle` = the only identifier, no separate name; `Items` jsonb via
  the value-converter pattern). The handle is normalized on create (`MenuService.NormalizeHandle`: "Footer Links" →
  `footer-links`, frontend mirror `normalizeHandle`) and never changes afterwards (site code loads menus by it).
  `MenuItem(Id, Label, PageId?, Url?, Children)`: page link, URL (absolute http(s) or same-site path `/about`,
  `MenuService.IsMenuUrl`; `//host` rejected), or neither = groups its children.
- `main` (`Menu.MainHandle`) = the public main navigation: seeded on startup by `MenuService.EnsureMainAsync` (after
  the pages, always on; Home + Login, Login only with `publicAuth`), can't be deleted.
- Validation (`MenuLimits`): depth ≤10, ≤200 items, unique item ids, label ≤100 (required unless page link: empty =
  page title), PageId xor Url, Url per `IsMenuUrl`, PageIds must exist.
- Public resolution (`GetPublicAsync`): page links → current slug/title, published only. A link to a draft/deleted page
  becomes a folder when labeled, else its children move up; folders without children are dropped.
- Errors reuse `PageResult<T>`/`PageError` (`SlugTaken` = handle taken).

## Public HTML (pre-rendered pages)
Why + full flow: `claude-context/ARCHITECTURE.md` → Public pages.
```
PublicHtmlController   /api/public/html      [AllowAnonymous], rate limit "pages", not in OpenAPI (nginx-facing)
  GET|HEAD /                   → home page HTML
  GET|HEAD /{slug}             → 200 stored HTML (weak ETag, no-cache, 304) | 404 rendered not-found page
                                 | 301 → / (`home`) | X-Accel-Redirect: /index.html (ReservedSlugs → admin SPA)
                                 | X-Accel-Redirect: /public.html (no fresh HTML yet → client-rendered shell)
```
- `Services/Rendering/`: `RenderQueue` (`IRenderQueue.Page(id)` / `All()`, called by Pages/Menus/SiteConfig/Media
  controllers after a successful write; `Requeue(id?)` = worker-internal, no invalidation), `RenderStatus`
  (singleton: renderer `CurrentVersion`, in-memory `NotFoundHtml`, change timestamps → `IsFresh`),
  `HttpRendererClient` (`IRendererClient`, typed HttpClient to the Node renderer; `RenderState` mirrors
  `frontend/src/public/state.ts`), `RenderWorker` (hosted: debounced render loop + version check every
  `CheckIntervalSeconds`; failures retry after 30 s).
- `Models/Rendering/RenderedPage.cs` → `rendered_pages` (PK/FK `PageId`, cascade): `Html`, the `Title`/`Slug` it was
  rendered with (a difference widens a one-page job to all), `RendererVersion`, `RenderedAt` (= render start, compared
  with change timestamps). Only rows of the current renderer version and newer than the last change are served.
- `Renderer:BaseUrl` empty → worker off, every page is the shell (integration tests, `dotnet run` without the renderer).

## Backups (`Services/Backup/`)
Design, archive layout and the restore sequence: `claude-context/ARCHITECTURE.md` → Backups.
```
BackupsController           /api/backups             [Authorize(SuperAdmin)], rate limit "auth" (general)
  GET    /settings             → BackupSettingsResponse (schedule + nextRunAt, directory, hostPath, freeBytes, maxUploadBytes)
  PUT    /settings             UpdateBackupSettingsRequest(interval off|daily|weekly|biweekly|monthly, timeOfDay "HH:mm" UTC,
                                 weekday 0-6, dayOfMonth 1-28, retention 1-100)
  GET    /                     → BackupInfo[] (name, kind from the file name, createdAt/cmsVersion/mediaCount from the manifest, error)
  POST   /                     → 201 BackupInfo (manual) | 409 busy | 500 tool/disk failure (detail = message)
  GET    /{name}               → application/gzip file (range) | 404
  DELETE /{name}               → 204 | 404 | 409
  POST   /upload               multipart `file` (no Kestrel cap; ≤ Backup:MaxUploadMegabytes) → 201 BackupInfo (upload-…) | 400 damaged/not a backup
  POST   /{name}/restore       → RestoreResult(preRestoreBackup) | 400 damaged/newer schema | 404 | 409 | 500
```
- `BackupService` (scoped): Create/List/Delete/SaveUpload/Restore/RunScheduledAsync, `BackupResult<T>` (`BackupError`:
  NotFound | Busy | Invalid), tool/disk failures throw `BackupFailedException` (controller → 500 with the message).
  Names only via `BackupArchive.NamePattern()` (no traversal); work files in `<dir>/.work/<guid>`, archives written as `.partial`.
- `BackupArchive` (layout, manifest record, `ReadManifestAsync` = first entry only, `ScanAsync` = full read + checks),
  `BackupSchedule` (pure due/next logic, unit-tested), `BackupScheduler` (hosted, 1-min tick: `DeleteExpired` (> 2 years,
  any kind, by the file-name timestamp `BackupArchive.StoredAt`) + `RunScheduledAsync`; tests remove it),
  `BackupLock` (singleton: `TryEnter`, `Maintenance()` → the Program.cs middleware answers other `/api` requests 503).
- `IDatabaseDumper` → `PgDumper`: `pg_dump --snapshot`, `pg_restore -f - | psql -1` (credentials via `PG*` env from
  `ConnectionStrings:Postgres`; ends psql's input only after pg_restore exited 0, else kills it = rollback).
- `Models/Backup/BackupSettings.cs` → `backup_settings` singleton (no FKs: survives restores), `BackupTables.DataExcluded`.
- `IBlobStorage.ListKeysAsync` (restore removes unreferenced blobs), `MediaService.ContentTypeOf(key)` (re-upload).

## Site config
One row (`site_config`, `Id = 1`, created on first PUT). Shaped by the instance config: `CmsConfig.SiteConfigSchema()` =
core field `firmName` + core group `contact` (presets address/phone/email, all default, custom entries allowed; an
instance definition with the same key replaces them) + the instance's `siteConfig.fields` / `siteConfig.groups`.
```
SiteConfigController        /api/site-config         [Authorize(StaffOnly)], rate limit "pages"
  GET    /                     → SiteConfigResponse
  PUT    /                     UpdateSiteConfigRequest(fields{}, groups{}, logoMediaId?, iconMediaId?) — full replace → SiteConfigResponse | 400
PublicSiteConfigController  /api/public/site-config  [AllowAnonymous], rate limit "pages"
  GET    /                     → SiteConfigResponse (footer, templates, favicon of the public site)
```
- Stored: `Values` jsonb `SiteConfigValues { Fields: {key: string}, Groups: {key: ConfigEntryValue[]} }` (value
  converter like `Page.Blocks`), `LogoMediaId`/`IconMediaId`/`ShareImageMediaId` (og:image fallback) → `media_assets`
  ON DELETE SET NULL, `UpdatedAt`/`UpdatedById`.
- Entry `ConfigEntryValue(Id, Key, Type, Value, Address?)`: key = a preset key (type comes from the preset) or the
  owner's own name (custom). Type `text|email|phone|link|address`; `address` (street, postal code, city, country) in
  `Address`, `Value` empty; fixed fields can't be address.
- Read (`SiteConfigService.Resolve`): fitted to the current schema: every field (empty if missing), every group (stored
  entries, or the default entries if never saved; ids = preset keys), required entries always present, unknown keys
  and disallowed custom entries dropped, labels + preset types applied → `ConfigEntry(Id, Key, Label, ...)`.
- Write (`Normalize`): known field/group keys only, required fields non-empty; per group ≤50 entries, key 1-50
  unique case-insensitively, custom only if `allowCustom`, required presets present, missing ids generated, link =
  absolute http(s), email = plain address, value ≤1000, address parts ≤200. Strings trimmed. Errors → 400
  `Invalid configuration.` with a detail naming the field/entry.
- `SiteConfigResponse.FooterLinks` (not stored): published pages marked `footer` in the instance config, in config order.

## Auth overview
Two independent user types, separate tables and controllers. Shared flows (login, refresh, logout,
password, profile) live in `PrincipalAuthService<TUser>`; both entities implement `IAuthPrincipal`.

| | Customer | Staff |
|---|---|---|
| table | `customers` | `staff` |
| controller | `CustomerAuthController` | `StaffAuthController` |
| route prefix | `/api/auth/customer` | `/api/auth/staff` |
| register | self-service (signs in) | SuperAdmin only (returns `UserInfo`, no session) |
| roles | none | many-to-many `staff_roles` |
| multi-tenancy | no | `OrganizationId` (nullable) |

## Endpoints (both controllers have same shape)
```
POST /register           (staff: requires [Authorize(Policy="SuperAdmin")])
POST /login
POST /refresh            (reads the refresh_token cookie; rotates it)
POST /logout             (revokes the cookie's token and clears the cookie)
GET  /me                 (customer: CustomerOnly | staff: StaffOnly)
POST /change-password    (customer: CustomerOnly | staff: StaffOnly) — verifies current password, updates hash, revokes all refresh tokens
POST /profile            (customer: CustomerOnly | staff: StaffOnly) — updates display name
```

### Email flows (customer only, anonymous; on `CustomerAuthController`)
```
POST /request-verification   issues an EmailVerification token, emails the link — always 204 (no enumeration)
POST /confirm-email          consumes the token, sets IsEmailConfirmed=true — 204 / 400 invalid|expired
POST /forgot-password        issues a PasswordReset token, emails the link — always 204 (no enumeration)
POST /reset-password         consumes the token, sets new password hash, revokes all refresh tokens — 204 / 400
```
Staff has no `IsEmailConfirmed` and is admin-provisioned, so these are customer-only. Login is **not**
gated on confirmation — confirming only flips the flag.

## JWT
- Access token: 15 min, signed HS256, claims: `sub` `email` `jti` `user_type` `role` `org_id`
- Refresh token: `SecureToken` (64 random bytes → base64url, SHA-256 hash stored), 30-day expiry.
  Sent only as an HttpOnly, SameSite=Strict `refresh_token` cookie scoped to `/api/auth/{customer|staff}`
  (Secure unless Development; `Auth:SecureCookie` overrides). Never in response bodies.
- Rotation is atomic (conditional `ExecuteUpdate`); reusing a rotated token more than 30 s later
  revokes all of the user's sessions (`RefreshTokenService`).
- Signing key: `Jwt:SigningKey` config (≥32 chars outside Development; known placeholder keys rejected)
- Login for unknown emails runs a dummy PBKDF2 verify (no timing enumeration); outdated hashes are
  rehashed on login.
- First super admin: `Bootstrap:SuperAdminEmail` / `Bootstrap:SuperAdminPassword` (`StaffBootstrapper`); fresh-install page seed: `Bootstrap:SeedPages`

## Authorization policies (Program.cs)
```
CustomerOnly  → user_type == "customer"
StaffOnly     → user_type == "staff"
SuperAdmin    → user_type == "staff" AND role == "super_admin"
```

## Services (Program.cs DI)
```
PasswordHashService   Singleton   PBKDF2 via ASP.NET Core Identity PasswordHasher
JwtTokenService       Singleton   access tokens (JsonWebTokenHandler), refresh-token generation
RefreshTokenService   Scoped      refresh-token issue / rotate / revoke / revoke-all
CustomerAuthService   Scoped      customer register + shared flows (PrincipalAuthService)
StaffAuthService      Scoped      staff register + shared flows (PrincipalAuthService)
AccountService        Scoped      customer email-verification + password-reset flows
CmsConfig             Singleton   instance config (CmsConfigLoader.Load), see Instance config
PageService           Scoped      CMS pages: CRUD, publish, slug rules, block validation, config page rules
MediaService          Scoped      media library: upload validation, alt, delete, resolve block media refs
SiteConfigService       Scoped      site config: get / replace, field + media validation
RenderQueue/IRenderQueue, RenderStatus  Singleton  public-page render jobs + freshness state
IRendererClient       Typed HttpClient  HttpRendererClient → Node renderer (Renderer:BaseUrl)
RenderWorker          Hosted      renders published pages to rendered_pages
MenuService           Scoped      menus: CRUD, item validation, public resolution, main-menu seed
IBlobStorage          Singleton   S3BlobStorage (tests: InMemoryBlobStorage)
EmailQueue/IEmailQueue Singleton  in-memory queue; EmailDispatcher (hosted) sends via IEmailSender
TokenCleanupService   Hosted      every 6 h deletes tokens expired/revoked > 7 days ago
BackupService         Scoped      full backups: create / list / upload / delete / restore / scheduled run
BackupLock            Singleton   one backup operation at a time + restore maintenance gate
IDatabaseDumper       Singleton   PgDumper (pg_dump / pg_restore / psql)
BackupScheduler       Hosted      every minute: automatic backup when due
IEmailSender          Singleton   ConsoleEmailSender (default, logs the link) when Email:Host empty; MailKitEmailSender (SMTP) otherwise
```

## Email (Services/Email/)
`IEmailSender` is the provider-agnostic seam (`EmailMessage` = To/Subject/Body, plain text). The
sender is chosen in `Program.cs` by config: `ConsoleEmailSender` when `Email:Host` is empty (dev/test:
the link is written to the API logs), else `MailKitEmailSender` (MailKit SMTP). `EmailOptions` binds
the `Email` section. Account emails are queued (`IEmailQueue`) and sent by `EmailDispatcher`, so
request latency doesn't depend on SMTP (or reveal whether an account exists).

`VerificationToken` (`Models/Auth/VerificationToken.cs`, table `verification_tokens`) is a hashed,
single-purpose token mirroring `RefreshToken`: SHA-256 hash at rest, `Purpose` enum
(`EmailVerification` | `PasswordReset`), `ExpiresAt`/`ConsumedAt`, owner `CustomerId XOR StaffId`
check constraint (`ck_verification_token_single_owner`). The raw token only ever lives in the emailed
link. `AccountService` issues/consumes them; issuing one consumes older unused tokens of the same
purpose. A successful reset also marks the email confirmed.

## Key files
```
Program.cs                          # DI registration, middleware, CORS, JWT config, auto-migrate
AppDbContext.cs                     # DbContext: Customers, Staff, Roles, StaffRoles, RefreshTokens, VerificationTokens, Pages, MediaAssets, SiteConfig, Tags, PageTags, Menus, RenderedPages
Models/Pages/Page.cs, Block.cs      # CMS page entity + block tree node
Models/Dto/Pages.cs                 # page DTOs
Services/Pages/PageService.cs       # page logic
Controllers/PagesController.cs      # staff page API
Controllers/PublicPagesController.cs  # anonymous published-page reads
Models/Media/MediaAsset.cs          # media entity (StorageKey, FileName, ContentType, Size, Alt)
Models/Dto/Media.cs                 # MediaItem, MediaRef, UpdateMediaRequest
Services/Media/                     # IBlobStorage, S3BlobStorage (+ S3StorageOptions), MediaService
Controllers/MediaController.cs, PublicMediaController.cs
Models/Site/SiteConfig.cs           # site config entity + SiteConfigValues, ConfigEntryValue, ConfigAddress, ConfigFieldType, ConfigLimits
Models/Dto/SiteConfig.cs            # SiteConfigResponse, UpdateSiteConfigRequest
Services/Site/SiteConfigService.cs, Controllers/SiteConfigController.cs, PublicSiteConfigController.cs
Models/Auth/Customer.cs             # entity
Models/Auth/Staff.cs                # entity (has OrganizationId, StaffRoles nav)
Models/Auth/Role.cs                 # entity (seeded: "super_admin", "staff")
Models/Auth/JWT/RefreshToken.cs     # entity (TokenHash, UserType, IsActive computed, RevokedAt)
Models/Dto/Auth.cs                  # auth request/response DTOs
Services/Auth/CustomerAuthService.cs
Services/Auth/StaffAuthService.cs
Services/Auth/JWT/JwtTokenService.cs
Services/Auth/JWT/PasswordHashService.cs
Migrations/                         # EF migrations, auto-applied on startup
appsettings.json                    # base config
appsettings.Development.json        # dev overrides
```

## Config keys (appsettings.json)
In docker these are overridden by env vars from `.env` (mapped via `Section__Key` in
docker-compose); host-side `dotnet run` uses the appsettings values below.
```
ConnectionStrings:Postgres          # injected from DB_* env vars in docker-compose
Jwt:Issuer                          # "website-template"          ← API_JWT_ISSUER
Jwt:Audience                        # "website-template-clients"  ← API_JWT_AUDIENCE
Jwt:SigningKey                      # from API_JWT_KEY env var
Jwt:AccessTokenMinutes              # 15                          ← API_JWT_ACCESS_MINUTES
Jwt:RefreshTokenDays                # 30                          ← API_JWT_REFRESH_DAYS
Cors:Origins                        # comma-separated string      ← CORS_ORIGINS
RateLimiting:PermitLimit/WindowSeconds/QueueLimit                ← RATE_LIMIT_* (credential endpoints)
RateLimiting:General:PermitLimit/WindowSeconds                   ← RATE_LIMIT_GENERAL_*
RateLimiting:Pages:PermitLimit/WindowSeconds                     ← RATE_LIMIT_PAGES_* (default 600/60 s)
ForwardedHeaders:KnownNetworks      # CIDRs trusted for X-Forwarded-For ← FORWARDED_KNOWN_NETWORKS
Auth:SecureCookie                   # refresh cookie Secure flag  ← AUTH_SECURE_COOKIE
Bootstrap:SuperAdminEmail/Password  # first super admin           ← BOOTSTRAP_SUPERADMIN_*
Email:Host/Port/User/Password                       # SMTP; empty Host → console sender  ← SMTP_*
Email:From                          # envelope from address       ← EMAIL_FROM
Email:FromName                      # display name                ← EMAIL_FROM_NAME
Email:LinkBaseUrl                   # frontend origin for email links (no trailing slash) ← APP_BASE_URL
Email:VerificationLinkExpiryHours   # 24
Email:ResetLinkExpiryHours          # 1
Storage:S3:ServiceUrl               # http://seaweedfs:8333 in docker-compose (required, validated on start)
Storage:S3:AccessKey/SecretKey      # ← S3_ACCESS_KEY / S3_SECRET_KEY (required)
Storage:S3:Bucket                   # "media"                     ← S3_BUCKET
Storage:S3:Region                   # "us-east-1" (signing only)
Renderer:BaseUrl                    # http://renderer:8080 in docker-compose; empty → no pre-rendering
Renderer:PublicBaseUrl              # site origin for canonical/OG URLs ← APP_BASE_URL
Renderer:DebounceMilliseconds       # 1000
Renderer:CheckIntervalSeconds       # 60
Backup:Directory                    # "/backups" (Development: "backups", relative to the content root)
Backup:HostPath                     # display only: host side of the mount  ← BACKUP_DIR
Backup:MaxUploadMegabytes           # 2048 (nginx /api/backups allows 2g)
Cms:Version                         # "dev"; release images: the tag (Dockerfile ARG CMS_VERSION), in backup manifests
Cms:ConfigPath                      # instance config file, relative to the content root (default cms.config.json)
```

## DTOs (Models/Dto/Auth.cs, Models/Dto/Pages.cs) — never expose entities directly
```
All strings have MaxLength (email 256, password/name 128, token 512).
RegisterRequest         Email, Password(min8), DisplayName
StaffRegisterRequest    : RegisterRequest + OrganizationId, Roles[]
LoginRequest            Email, Password
AuthResponse            AccessToken, AccessTokenExpiresAt, UserInfo   (refresh token → cookie)
UserInfo                Id, Email, DisplayName, UserType, Roles[], OrganizationId
ChangePasswordRequest   CurrentPassword, NewPassword(min8)
UpdateProfileRequest    DisplayName
RequestVerificationRequest  Email
ConfirmEmailRequest         Token
ForgotPasswordRequest       Email
ResetPasswordRequest        Token, NewPassword(min8)
PageSummary / PageDetail    Id, Title, Slug, Status, Tags, BlockCount | Blocks + Media, CreatedAt, UpdatedAt, PublishedAt,
                            Locked (summary) | Template?, Locked, Editable (detail; from the instance config), MetaTitle, MetaDescription
CreatePageRequest           Title(≤200), Slug?(≤100), Blocks?
ImportPageRequest           Title, Slug?, Blocks, MetaTitle?, MetaDescription?, Tags?   ImportPageResult  Page, MissingMediaIds
UpdatePageRequest           Title?, Slug?, Blocks?, MetaTitle?(≤70), MetaDescription?(≤200)   (partial)
PublicPage                  Title, Slug, Blocks, Media, PublishedAt, Template?, MetaTitle, MetaDescription
InstanceConfig              PublicAuth, ExcludedBlocks, SiteConfig (SiteConfigSchema: fields[], groups[])
UpdatePageTagsRequest       Tags[] (≤20)   TagUsage  Name, PageCount
MenuSummary / MenuDetail    Id, Handle, ItemCount | Items, CreatedAt, UpdatedAt
CreateMenuRequest           Handle(≤50)   UpdateMenuRequest  Items
PublicMenu / PublicMenuItem Handle, Items | Label, Slug?, Url?, Children
MediaItem                   Id, Url, FileName, ContentType, Size, Alt(≤100), CreatedAt
MediaRef                    Id, Url, Alt   (page responses)
UpdateMediaRequest          Alt?
BackupSettingsResponse    Interval, TimeOfDay, Weekday, DayOfMonth, Retention, LastRunAt?, LastError?, NextRunAt?, Directory, HostPath?, FreeBytes?, MaxUploadBytes, MaxAgeYears
UpdateBackupSettingsRequest  Interval, TimeOfDay("HH:mm"), Weekday(0-6), DayOfMonth(1-28), Retention(1-100)
BackupInfo                Name, Kind (auto|manual|pre-restore|upload), CreatedAt, Size, CmsVersion?, MediaCount?, Error?   RestoreResult  PreRestoreBackup
SiteConfigResponse        Fields {key: value}, Groups {key: ConfigEntry[]}, Logo?/Icon?/ShareImage? (MediaRef), UpdatedAt?, FooterLinks
ConfigEntry               Id, Key, Label, Type, Value, Address?
UpdateSiteConfigRequest   Fields {key: value}, Groups {key: ConfigEntryValue[]}, LogoMediaId?, IconMediaId?, ShareImageMediaId?   (full replace)
```

## Adding a feature
1. Entity in `Models/` + configure in `AppDbContext.cs`
2. `dotnet ef migrations add <Name>` → commit migration files
3. DTO in `Models/Dto/`
4. Service in `Services/` → register Scoped in `Program.cs`
5. Controller in `Controllers/` → inject service, apply `[Authorize(Policy="...")]`

## DB tables
`customers`, `staff`, `roles` (seeded), `staff_roles` (junction), `refresh_tokens`,
`verification_tokens`, `pages` (AddPages migration: unique `Slug`, index on `Status`, `Blocks` jsonb,
`CreatedById`/`UpdatedById` → staff ON DELETE SET NULL), `media_assets` (AddMediaAssets: unique
`StorageKey`, index on `CreatedAt`, `CreatedById` → staff ON DELETE SET NULL), `site_config` (AddSiteConfig: singleton,
logo/icon → media_assets SET NULL; SiteConfigValues: `Values` jsonb replaced the free `Fields` list, old data dropped;
AddPageMetaAndShareImage: `pages.MetaTitle/MetaDescription`, `site_config.ShareImageMediaId`), `tags` + `page_tags` and `menus` (AddTagsAndMenus: unique tag `Name`,
unique menu `Handle`, `Items` jsonb), `rendered_pages` (AddRenderedPages: PK/FK `PageId` → pages CASCADE),
`backup_settings` (AddBackupSettings: singleton, enums as strings, no FKs)
Constraints: email unique per table; refresh_token + verification_token owner check
(CustomerId XOR StaffId non-null)

## Hardening

### Global exception handler
`GlobalExceptionHandler.cs` implements `IExceptionHandler`. All unhandled exceptions return RFC 7807 `application/problem+json` with `status`, `title`, `traceId`. Exception details are included only in Development.

### Rate limiting (`RateLimitPolicies`)
Fixed-window limiters partitioned by client IP (real IP via `UseForwardedHeaders`). Returns `429`
with `Retry-After`.
- `auth-credentials` — login, register, email flows, change-password: `RateLimiting:*` (default 10/60 s)
- `auth` — controller default (refresh, logout, me, profile): `RateLimiting:General:*` (default 120/60 s)
- `pages` — page editor + public page reads: `RateLimiting:Pages:*` (default 600/60 s; the editor autosaves)

Account lockout is intentionally out of scope (no schema change).

### Other hardening
Security headers (`nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Cache-Control: no-store`
on `/api/auth`), 64 KB request body limit (2 MB on page writes via `[RequestSizeLimit]`), Swagger UI only in Development.

### Health check
`GET /health` returns `200 Healthy` when Postgres is reachable, `503 Unhealthy` otherwise. Uses `AspNetCore.HealthChecks.NpgSql`.

### Swagger Bearer auth
The "Authorize" button in `/swagger` accepts a JWT. Paste an access token to call protected endpoints.

### Middleware order (Program.cs)
`UseForwardedHeaders` → `UseExceptionHandler` → security headers → restore gate (503) → `UseSwagger`/`UseSwaggerUI` (dev) → `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseRateLimiter` → `MapControllers` + `MapHealthChecks`
(No `UseHttpsRedirection`: TLS terminates at the proxy.)

## Testing (`Tests/`)
xUnit v3 + AwesomeAssertions + Testcontainers.PostgreSql (`Tests/Tests.csproj`, net10.0).
```
Unit/Services/        JwtTokenService, PasswordHashService, Customer/StaffAuthService, PageService, CmsConfigLoader, BackupSchedule
                      (EF InMemory via Unit/TestDbContextFactory.cs; ExecuteUpdate-based
                      flows such as refresh rotation are covered by integration tests)
Integration/          ApiFactory.cs (WebApplicationFactory + Testcontainers PostgreSQL),
                      CustomerAuthTests, StaffAuthTests, HealthCheckTests,
                      RateLimitingTests, ExceptionHandlerTests, EmailFlowTests,
                      BootstrapTests, TokenCleanupTests, PagesTests, MediaTests, SiteConfigTests, TagsTests, MenusTests (AuthCookies.cs: refresh-cookie helpers),
                      InstanceConfigTests (locked/template pages, footer links, public auth off; own CmsConfig via ConfigureTestServices),
                      RenderingTests (FakeRenderer replaces IRendererClient; fast debounce/check config),
                      BackupsTests (auth, settings, names, busy 409, 503 gate, invalid uploads, newer schema refused),
                      BackupRoundTripTests (real pg tools, own DB: backup → change → restore → undo, download/upload,
                      scheduled run + retention; skipped without pg_dump)
                      (CollectingEmailSender + InMemoryBlobStorage test doubles in ApiFactory)
```
Run: `dotnet test --project api-backend/Tests/Tests.csproj` (needs Docker for the Testcontainers PostgreSQL).
No local SDK? See the `docker run ... mcr.microsoft.com/dotnet/sdk:10.0` recipe in the root `CLAUDE.md`.
New rate-limit policies need a high override in `Integration/ApiFactory.cs` (tests run from one IP).
`ApiFactory` gives each factory its own `Backup:Directory` (temp, deleted on dispose) and removes `BackupScheduler`.
Tests that build their own `WebApplicationFactory` must add `ApiFactory.StorageSettings` (S3 start validation), and
`ApiFactory.Cms` (defaults + public auth on) when they call customer endpoints. `ApiFactory` registers `ApiFactory.Cms`.
Tests run on Microsoft.Testing.Platform (xunit.v3 4.x); the root `global.json` opts `dotnet test` into it.
Integration tests boot the real app against a throwaway Postgres container; unit tests mock the
DbContext. Root alias: `pnpm test:back`.

## Scripts
```bash
dotnet run                              # starts API, auto-applies pending migrations
dotnet build
dotnet test --project Tests/Tests.csproj  # MTP mode (see root global.json)
dotnet ef migrations add <Name>         # create new migration after entity changes
```

## OpenAPI contract generation
The committed contract `frontend/openapi/swagger.json` is regenerated on **every build** by
`Microsoft.Extensions.ApiDescription.Server` (csproj: `OpenApiDocumentsDirectory` →
`../frontend/openapi`; the `NormalizeOpenApiFileName` target renames the emitted
`api-backend.json` to `swagger.json`). `pnpm build:back` / `dotnet build` both trigger it.

The contract is **exact**, so the frontend uses the generated types as-is (no mapping layer):
`SupportNonNullableReferenceTypes` + `NonNullableReferenceTypesAsRequired` + `UseAllOfToExtendReferenceSchemas`
in `AddSwaggerGen` → non-nullable properties (value types included) are `required`, `?` ones `nullable`.
Declare DTO nullability deliberately. Enums on the wire (`PageStatus`, `ConfigFieldType`) carry
`[JsonConverter(typeof(JsonStringEnumConverter<T>))]` + lowercase `[JsonStringEnumMemberName]` → string
union in TS, same values in jsonb and in tests' `ReadFromJsonAsync`.

That generator (`dotnet getdocument`) boots the app in-process to enumerate endpoints, so
`Program.cs` detects it via `Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"`
(`generatingOpenApi`) and relaxes startup when true: a dummy connection string, the dev JWT
fallback, no S3 options validation, and **skipping** migrations and the super-admin bootstrap. Production startup stays strict.

The root `Dockerfile`'s `api-build` stage builds with `-p:OpenApiGenerateDocumentsOnBuild=false`: the frontend package
ships the client generated from the committed contract (`frontend/src/api/`, CI checks both for drift), so the image
build doesn't generate anything. Contract change → `pnpm build:back` → `pnpm gen-api` → commit both.
