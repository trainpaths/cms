# Phase 2: per-language public site

Status: **not started**. Read `README.md` (this folder) first. Issue trainpaths/cms#45. Starts after PR #46 merged.

## Goal
On a localized site every public page and customer screen exists once per language: `/about` (default language),
`/de/about`, `/fr/about`, `/de/login`, `/de/user/profile`, `/de` (German home). Each is server-rendered and stored in
that language (UI strings via the Phase 1 messages; page **content** is still the one shared copy until Phase 3). The
public `<LanguageDropdown />` switches to the same page in another language and remembers the choice in `cms_lang`;
an unprefixed URL redirects to the remembered language. Search engines see every language (`hreflang`).
Single-language sites: no change at all (no prefixes, no redirect, no `Vary`, one row per page).

## Out of scope
Translated page content (Phase 3), localized slugs, menu label translation, `Accept-Language` negotiation.

## Design

### URLs and routing (API: `Controllers/PublicHtmlController.cs`, nginx)
- nginx (`frontend/nginx.conf`, shipped to instances): change the public location to
  `location ~* "^/([a-z]{2,3}/)?(user/)?[a-z0-9-]*$"` (same rewrite to `/api/public/html/$1`). `/de` alone already
  matches the single-segment form.
- `PublicHtmlController` (inject `CmsConfig`, it already has it): add routes `{lang}/{slug}` and `{lang}/user/{name}`,
  and resolve the first segment in `BySlug` too (`/de` → German home). Resolution order for a path:
  1. First segment is a configured **non-default** code → language = that code, rest = slug/screen (empty = home).
  2. First segment is the **default** code (`/en/about`) → `301` to the unprefixed path.
  3. Otherwise → default language; if the request has a `cms_lang` cookie naming another configured code, `302` to the
     prefixed path (keep the query string). Only for page/screen/home paths, never for reserved admin slugs.
  - Prefixed reserved non-screen slug (`/de/admin`) → `301` to the unprefixed path (admin SPA). Prefixed `home`
    (`/de/home`) → `301` `/de`.
  - On a localized site add `Vary: Cookie` to every response of this controller (stored HTML is `no-cache` already).
  - Single-language site: steps 1–3 never apply (no codes are prefixes, no redirect, no Vary).
- Language codes must never be page slugs: `PageService.ValidateSlug` is static and used by `CmsConfigLoader`; add the
  check at the instance level instead: `PageService` (scoped, inject `CmsConfig`) rejects create/rename/import slugs equal
  to a configured code (`Slug '{slug}' is reserved.`, same message as `ReservedSlugs`), and `UniqueSlugFromAsync` treats
  them as reserved (generated slug gets `-page`). `CmsConfigLoader.ValidateLang` already rejects codes that equal a
  configured page slug or a `ReservedSlugs` entry. An existing page whose slug equals a newly added code: log a warning
  on startup (it becomes unreachable); don't auto-rename.

### Render state contract
- `PublicState` (`frontend/src/public/state.ts`) and its mirror `RenderState` (`Services/Rendering/RendererClient.cs`)
  gain `lang: string` (code) — required, the renderer always gets it. The language list itself comes from
  `__CMS_LANGUAGES__` in the bundle (same `cms.config.json`), not from the state.
- `pathOf(slug)` gets a sibling `localePath(path, code)` in a new `frontend/src/public/locale.ts`:
  `'/about','de'` → `/de/about`; default code → unchanged; `'/'` → `/de`. Also `stripLocale(pathname)` →
  `{ code, path }` (unknown/default prefix → default). Unit-test both.
- Public JSON endpoints (`PublicPagesController` `GET /api/public/pages/{slug}`, `PublicStateController`
  `GET /api/public/state[/{slug}]`) accept `?lang=` (unknown → default) and pass it through to the service. In this phase
  it only matters for the footer links once Phase 3 translates titles, but adding it now keeps the client code final.
  OpenAPI changes → `pnpm build:back` + `pnpm gen-api`.

### Rendering (API: `Services/Rendering/`, `Models/Rendering/RenderedPage.cs`, `AppDbContext`)
- `RenderedPage` gains `Lang` (string, max 3, required); primary key becomes `(PageId, Lang)` (FK to pages stays,
  cascade). New migration `RenderedPageLang`: delete all rows first (`migrationBuilder.Sql("DELETE FROM rendered_pages")`
  — the table isn't in backups and everything re-renders on start because the renderer version check runs at boot),
  add the column, swap the key.
- `RenderWorker.RenderAsync`: languages = `cms.Site.Lang.Tags` mapped to codes (single-language → just the default).
  - Widen check (`!all` branch): join `RenderedPages` on `(PageId, default code)` only; same rules as now.
  - Per chunk: `existing` = `HashSet<(Guid, string)>` of rows for the chunk; for each page, for each language: build
    `new RenderState(page.Slug, page, menu, config, baseUrl, code)`, render, `Track` with the language. One commit per
    chunk as now (chunk size stays 20 pages, so ≤ 20 × N renders per commit). `StoreAsync` fallback per `(id, code)`.
  - Cleanup when `all`: also delete rows whose `Lang` isn't configured any more.
  - Not-found page and screens: render per language. `RenderStatus.NotFoundHtml` → `IReadOnlyDictionary<string, string>`
    keyed by code; `RenderedScreens.Html` → keyed by `(code, path)` (or `"de:user/profile"`). Keep `CurrentVersion`
    from the default language's not-found render.
  - Log line: `Rendered {Count} public page(s) in {Languages} language(s)…`.
- `PublicHtmlController.ServeAsync(slug, code)`: join `RenderedPages` on `PageId` **and** `Lang == code`;
  `Screen(path, code)`, not-found per code (`FallbackNotFound` stays English).
- `RenderStatus.IsFresh / NeedsRefresh / MarkChanged`: unchanged (per page, all languages go stale together).
- `PublicCache`: unchanged in this phase (menus and site config aren't translated).

### Frontend public app (`frontend/src/public/`, `frontend/server/`)
- **Router base = language prefix.** `createPublicRouter(history, …)` stays as is; the histories get a base:
  `createWebHistory(code === default ? '/' : `/${code}/`)` in `entry-client.ts` and `createMemoryHistory(base)` in
  `entry-server.ts`. All route paths (`/login`, `/user/profile`, `{ name: 'home' }`, menu `/about` URLs in `SiteNavLink`)
  then resolve with the prefix automatically: **no changes to SiteHeader/SiteFooter/SiteNavLink/screens/NotFound
  links**. Check the places that bypass the router:
  - `router.ts` `window.location.assign(to.fullPath)` → `router.resolve(to).href` (fullPath has no base).
  - `entry-client.ts` `fetchState` reads `location.pathname`: strip the prefix with `stripLocale` first; fetch with `lang`.
  - `store.ts` `load(slug)`: pass `lang` to `getApiPublicPagesBySlug` (`query: { lang }`).
  - `redirect.ts` `safeRedirect`: `redirect` values are router paths (without base) since the router adds `?redirect=` from
    `from.fullPath` — keep it that way; make sure a prefixed value (`/de/about`, from an old link) is also accepted and
    normalized (strip the prefix).
  - `AdminBar.vue` plain `<a href="/admin">` stays unprefixed (correct). `NotFound.vue` `<Link to="/">` resolves to the
    language home (correct).
  - Links from the public app into `/admin` through the router (`external` route) → full page load to the unprefixed
    path; the API also 301s `/de/admin` → `/admin`.
- `createPublicApp(state, …)`: `const language = languageOf(state.lang)`; i18n for it; provide the real switch:
  `change(code) { rememberLanguage(code); location.assign(localePath(currentRouterPath, code)) }` (full page load:
  fresh stored HTML and head tags in the new language; no client-side language swap).
- `entry-server.ts`: route with the memory history base; `render()` returns the language tag too, and
  `server/render-server.js` / `server/dev-ssr.js` apply `setHtmlLang(html, tag)` (from `server/site-lang.js`) to the
  spliced document per render (the template's build-time `lang` is the default).
- `server/dev-ssr.js`: parse the prefix (`/de/about`, `/de/user/profile`, `/de`) the same way as the API, fetch
  `/api/public/state[/{slug}]?lang=`, pass `lang` into `render()`; unprefixed + cookie → redirect like the API.
- `head.ts` `headTags(state, t)`: canonical = `baseUrl + localePath(pathOf(slug), state.lang)`; for pages add
  `<link rel="alternate" hreflang="{tag}" href="…">` for every language plus `hreflang="x-default"` (the unprefixed URL),
  only when `IS_LOCALIZED`; `og:locale` = tag with `_` (`de_CH`), `og:locale:alternate` for the others.
- `SiteFooter.vue`: when `IS_LOCALIZED`, render `<LanguageDropdown align="end" />` in the bottom row (also when there are
  no footer links: the row then shows only the dropdown). The footer's `visible` rule must count it.

### Admin side
- Links from the admin to public pages (Pages list "view" eye, editor header eye, `PagePreview`, AdminBar) stay
  default-language URLs. Optional nicety (skip if it grows): the Pages list eye opens the page in the admin's current
  language (`localePath`).

## Tasks
1. API: migration + `RenderedPage.Lang`, worker per language, `RenderStatus` per language, controller routing + cookie
   redirect + `Vary`, `?lang` on public JSON endpoints, slug reservation of codes. xUnit as below.
2. `pnpm build:back` + `pnpm gen-api` (commit `swagger.json` + `src/api/`).
3. Frontend: `locale.ts`, `state.ts` + `RenderState` `lang`, router base in both entries, `store.ts`/`fetchState`/
   `redirect.ts`, `createPublicApp` switch, `head.ts` alternates, `SiteFooter` dropdown, per-render `<html lang>` in
   `render-server.js` + `dev-ssr.js`, nginx regex.
4. Tests + docs below.

## Tests
- xUnit `Integration/RenderingTests.cs` (FakeRenderer records states): rows per page × language, `lang` in each state,
  not-found/screens per language, cleanup of an unconfigured language, single-language config renders one row.
- xUnit new `Integration/PublicHtmlLanguageTests.cs` (own `CmsConfig` via `ConfigureTestServices`, see
  `InstanceConfigTests`): `/de/about` serves the German row; `/en/about` → 301 `/about`; `/xx/about` → 404; `/about` with
  `cms_lang=fr` → 302 `/fr/about` + `Vary: Cookie`; `/about` with `cms_lang=es` → 200; `/de/admin` → 301 `/admin`;
  `/de` → German home; single-language config: no Vary, `/de/about` → 404.
- xUnit `PageService`: a configured code as slug → 400; generated slug from a title equal to a code gets `-page`.
- Vitest `public/render.test.ts`: German render (`lang: 'de'`) → German strings, `<html lang="de-CH">` after splice,
  canonical `/de/about`, hreflang for all four + x-default; router with base `/de/` resolves `{ name: 'home' }` to `/de`
  and `/login` to `/de/login`; `locale.ts` unit tests; single-language state renders without alternates.
- e2e `playground/e2e/tests/site/languages.spec.ts`: footer dropdown on `/privacy-policy` → `/de/privacy-policy` with
  German footer/strings and `cms_lang=de`; visiting `/privacy-policy` again → redirected to `/de/…`; `/de/login` form in
  German; view-source (`request.get`) of `/fr/` has `lang="fr-CH"` and French strings; no hydration warnings in the
  console. Other specs must not be affected (they never set the cookie).

## Docs to update
`claude-context/ARCHITECTURE.md` (Public pages: routing, rendered_pages key, freshness unchanged; Localization: mark
Phase 2 done), `frontend/CLAUDE.md` (public site file map, router base, head), `api-backend/CLAUDE.md` (controller table,
`rendered_pages`, render worker), `frontend/docs/INSTANCE_GUIDE.md` (URLs per language, `LanguageDropdown` in the default
footer, overrides that build their own hrefs must use `localePath`; instances must take the new `nginx.conf`),
`playground/CLAUDE.md`, `claude-context/PROJECT_OVERVIEW.md`, this file's status line, #45 checkboxes.
