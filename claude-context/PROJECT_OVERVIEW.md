# Project Overview — CMS

## What this is
A content management system where **website owners** build pages from blocks and **anyone** can read
the published result. The editor is a recreation of WordPress Gutenberg's *block logic* (not WordPress
compatible, not its UI code): everything is a block, blocks nest, block types are registered with a
schema, and the whole page is one JSON tree.

It's a **framework**: each client site is its own instance repo that imports this one (frontend package + API
image) and adds blocks, overrides, theme and config (`ARCHITECTURE.md` → Framework and instances).

It grew out of two projects:
- `vue-csapi-template` — Vue 3 + .NET 10 + Postgres starter with Customer/Staff JWT auth (this repo's base)
- `gutenberg-copy` (NuxtBlocks) — a standalone Nuxt 4 + MongoDB block editor, ported here to plain Vue
  (`frontend/src/lib/web-editor/`) with its API moved into the .NET backend and its storage into Postgres

## Who uses it
| Role | Account | Can |
|------|---------|-----|
| Site owner | **Staff** (first one bootstrapped from `BOOTSTRAP_SUPERADMIN_*`; super admins create more) | log in, create/edit/delete pages, publish/unpublish |
| Visitor | none | read published pages at `/{slug}` |
| Customer | Customer (self-registration, only with `publicAuth` in the instance config) | currently nothing CMS-specific — kept from the template for future member features |

## Feature map
| Feature | Where |
|---------|-------|
| Staff login, sessions, password flows | template auth (`api-backend/Controllers/*AuthController.cs`, `frontend/src/stores/auth.ts`) |
| Page list / create / view / delete | `/admin/pages` → `frontend/src/views/admin/Pages.vue` |
| Admin navigation (left sidebar; full-screen burger menu on phones) + admin bar above the public site for staff | `frontend/src/components/AppNav.vue`, `components/AdminBar.vue` |
| Instance config (`cms.config.json`: public auth, hidden blocks, seeded pages incl. locked/template pages) | `api-backend/Services/Cms/`, `/api/public/instance`, schema `frontend/cms.config.schema.json` |
| Config pages (default: Home, Privacy Policy, Legal; seeded on a fresh install, locked/template ones on every start) | `api-backend/Services/Pages/{PageSeeder,DefaultPages}.cs` |
| Template pages (instance `src/templates/<name>.vue` renders the page; slug fixed, undeletable) | `frontend/src/public/templates.ts`, `views/PageView.vue` |
| Block editor (3-panel: Blocks/Outline tabs, canvas, settings) | `/admin/pages/:id` → `lib/web-editor/editor/Editor.vue` |
| Draft preview | `/admin/pages/:id/preview` |
| Page settings (slug, meta title/description, tags, publish) | right sidebar when no block is selected (`editor/PageSettings.vue`) |
| Public page rendering | Server-rendered on content change, hydrated: `frontend/src/public/` (Vue SSR) via the `renderer` service; API `RenderWorker` + `PublicHtmlController` (see ARCHITECTURE.md → Public pages) |
| Pages API | `api-backend/Controllers/PagesController.cs` (staff), `PublicPagesController.cs` (anonymous) |
| Media library (upload, rename, alt text, delete) | `/admin/media` → `views/admin/Media.vue`; in the editor: Upload / Choose existing on image + card blocks |
| Site config ("Configuration": fixed fields + groups shaped by the instance config, e.g. firm name, contact, socials; logo, icon) | `/admin/configuration` → `views/admin/Configuration.vue` (+ `components/config/`); public footer `components/SiteFooter.vue` (firm + contact + footer pages), public favicon |
| Site config API | `SiteConfigController.cs` (staff GET/PUT), `PublicSiteConfigController.cs` (anonymous GET) |
| Full backups (super admins; Profile → Backups tab): DB + media archives, daily/weekly schedule + retention, back up now, download, upload, restore (pre-restore backup first, signs everyone out) | `api-backend/Services/Backup/`, `/api/backups`, `components/backups/BackupsPanel.vue`; design ARCHITECTURE.md → Backups |
| Page JSON export/import (Export in a page row's "…" menu + editor Export section; Duplicate in the same menu = import of a copy; "Import" in the "+ New Page" row creates a new draft, slug deduplicated, media by id: unknown ids reported) | `frontend/src/lib/pageExport.ts`, `views/admin/Pages.vue`; API `POST /api/pages/import` |
| Page tags (editor Page tab, pages-list modal; autocomplete + 3 most popular) + page search / tag filter / status filter (pages list only) / updatedAt sort | nb-ui `TagInput`, `components/PageFilters.vue`; API `PUT /api/pages/{id}/tags`, `GET /api/tags` |
| Menus (nested links/folders; fixed **main** menu = public navigation, extra menus e.g. for a footer) | `/admin/menus` → `views/admin/Menus.vue`; public `components/SiteHeader.vue`; API `MenusController.cs`, `PublicMenusController.cs` |
| Media API + storage | `MediaController.cs` (staff), `PublicMediaController.cs` (anonymous file reads) → SeaweedFS (S3) |

Block types today: **card** (container, accepts link), **heading**, **paragraph**, **image** (from the media library),
**list** (accepts list-item), **list-item**, **link**.

## Roadmap / open items
- **Media follow-ups** — usage tracking ("used on N pages", block delete when in use), image resizing /
  thumbnails, non-image files. External image URLs are not supported (CSP `img-src 'self'`).
- **Customers** — no CMS role yet; decide if they become members/commenters or get removed.
- **Page hierarchy** — not started. SEO: per-page meta title/description exist (PageSettings); no per-page OG image
  (first image, else the site's share image) or noindex yet. Menus + public main nav exist; a footer menu would render another
  handle the same way (`usePublicMenusStore().load('<handle>')`).
- **Site config follow-ups** — SVG logos/icons (needs sanitizing), using config fields inside blocks, icons for presets (socials).
- **Concurrent editing** — last write wins (no optimistic concurrency yet).
