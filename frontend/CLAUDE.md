# frontend — the `@trainpaths/cms` package

Vue 3 + TypeScript + Tailwind v4 + Vite + hey-api + vue-router + Pinia.
The block editor lives in `src/lib/web-editor/` → see its `CLAUDE.md`.
**Two apps, split by audience:** the staff-only admin SPA (`createAdmin()` in `src/main.ts`, `/admin*`) and the
server-rendered public site (`src/public/`): pages + the customer screens (login, register, password flows, account
pages under `/user/*`). Why/how: `claude-context/ARCHITECTURE.md` → Auth model, Public pages.

**Instance developer guide:** `docs/INSTANCE_GUIDE.md` (in `files`, read from node_modules by instance repos) — update it
with every change to `cms.config.json`, exports, extension/override points or the image setup.

**A package, not an app:** shipped as raw source (no build step, like nb-ui), installed by instance apps from a git
tag (`github:trainpaths/cms#vX.Y.Z&path:/frontend`); in this repo `playground/` uses it via `workspace:*`. The
instance owns `index.html`, `public.html`, `vite.config.ts` (`plugins: [cms()]`), `src/style.css` (imports
`@trainpaths/cms/style.css` + its site theme), `cms.config.json` (read by the API; `cms()` reads only `site.lang` and
`theme.colors`, at build time) and three stubs that import the stylesheet and call the package entries. Why: `claude-context/ARCHITECTURE.md` → Framework and instances.

## Package surface (`package.json` exports)
```
./admin            src/main.ts                createAdmin(): Pinia, API base URL, session restore, router, mount #app
./public/client    src/public/entry-client.ts hydratePublic(): hydrate #__STATE__ or render the fallback shell
./public/server    src/public/entry-server.ts render(state) for the SSR bundle (instance: src/entry-server.ts)
./editor           src/editor.ts              block authoring, edit side (defineBlock, useBlockAttribute, useBlockText, InlineToolbar,
                                              ToolbarDropdown, SettingsSection, BlockList, Media*, useEditorStore, types)
./site             src/site.ts                public side for views/templates/overrides, no editor code (ViewBlockList,
                                              PageContent, useMediaImage, site config/menu stores, site config helpers
                                              from lib/siteConfig.ts + SiteConfigEntry, LanguageDropdown + LANGUAGES,
                                              DEFAULT_LANGUAGE, IS_LOCALIZED, languageLabel, useLanguageSwitch,
                                              localePath, TemplateProps, types)
./style.css        src/style.css              the CMS stylesheet (Tailwind + nb-ui theme + editor CSS + cms-* tokens)
./cms.config.schema.json                      JSON schema of the instance config (`"$schema"` in cms.config.json)
./vite             vite-plugin.js (+ .d.ts)   cms(): see below
./ui               src/ui.ts                  nb-ui re-export (instances still install nb-ui: peer dependency)
./tsconfig.json    tsconfig.base.json         compiler options the source is written for; instance tsconfigs extend it
                                              (their vue-tsc also checks the package source they import)
./server/*, ./nginx.conf                      renderer + nginx config for the instance's Docker images
./src/*                                       deep imports, e.g. wrapping an overridden component
```
**`cms()` Vite plugin** (`vite-plugin.js`, plain JS: Node won't strip types in node_modules, and Vite's config loader
externalizes deps; `server/*.js` are JS for the same reason, typed via JSDoc + `checkJs` in `tsconfig.node.json`):
vue + tailwind + `nbUi({ dir: 'src/overrides/ui' })` + dev SSR (`server/dev-ssr.js`); inputs `index.html` +
`public.html` of the app root; `ssr.noExternal` (build: all, dev: this package + nb-ui); `/api` proxy to
`VITE_API_BASE_URL` (app's envDir) or `cms({ apiTarget })`; `resolve.dedupe` vue/router/pinia; package `public/`
files (CMS favicon) served/emitted unless the app's publicDir has them; `transformIndexHtml` sets `<html lang>` of both
HTML files to the default language of `cms.config.json` `site.lang` (`server/site-lang.js`, read per transform so dev
picks up edits), all language tags → `define` `__CMS_LANGUAGES__` (`src/i18n/languages.ts`; dev server restarts when they
change) + vue-i18n's esm-bundler flags;
`theme.colors` (`server/theme.js`) → `<style data-cms-theme>` with `--color-<key>` on `:root .site-theme` in both HTML
files (outranks the instance's `.site-theme` rules) + `define` `__CMS_THEME_COLORS__` (editor palette,
`lib/web-editor/core/colors.ts`); the dev server restarts when the palette changes (watches cms.config.json).
**Instance extension points** (conventions in the app's `src/`):
- `src/blocks/<folder>/` — app blocks, same format as built-ins. `core/blockTypes.ts` / `core/blockViews.ts` glob
  `/src/blocks/*` (root-relative = the app's Vite root) after the built-ins: a block with a built-in's `name`
  replaces it (a folder with only `block.json` + `*View.vue` replaces just the view).
- `src/overrides/<Name>.vue` — replaces the package's `<Name>.vue` wherever the package imports it (resolveId on
  importers inside the package; basenames are unique). Wrap the original via `@trainpaths/cms/src/<path>.vue`.
  Needs a dev server restart when added/removed. nb-ui components: `src/overrides/ui/<Name>.vue`.
- `src/templates/<name>.vue` — template pages: a page whose config entry has `"template": "<name>"` renders with it
  (`public/templates.ts` globs `/src/templates/*.vue`; `views/PageView.vue` picks template or `PageContent`, public
  app + admin preview). Props `{ page, config }` (`TemplateProps`); `PageContent` has `before`/`after` slots for
  wrapping the owner's blocks. Missing file → default layout + console warning.
- Theme: the app's `src/style.css` overrides tokens under `.site-theme` (public root, preview, editor canvas `<main>`),
  new tokens in `@theme`. Editor chrome inside the canvas uses `ring-cms-primary` / `bg-cms-primary` / `font-cms`
  (`--color-cms-primary`/`--font-cms` resolve at :root, before the `.site-theme` override) to keep the admin look.
- Tailwind: `src/style.css` `@source`s the package (`.`, minus `api/`); the app root is auto-scanned.
- `src/locales/<code>.json` — UI strings deep-merged over the package's (`src/i18n/index.ts`, root-relative glob):
  reword package strings, add the instance's own, or a language the package doesn't ship. See Localization below.
- Instance config (`cms.config.json`): only the API reads it at runtime (`site.lang`: `cms()` sets `<html lang>` of
  index.html + public.html + the language list at build time, `server/site-lang.js`, invalid config fails the build); the admin loads `/api/public/instance`
  (`stores/instance.ts`, in `createAdmin()`): `excludedBlocks` (`isOffered(name)`, used by `Inserter.vue` / `BlockInserterSidebar.vue`). Locked/template/editable page flags
  come with `PageSummary`/`PageDetail` (slug + delete disabled, non-editable → notice instead of the canvas).

## Localization (`src/i18n/`, `src/locales/`; why: ARCHITECTURE.md → Localization)
```
i18n/languages.ts   LANGUAGES [{code, tag}] (default first, from __CMS_LANGUAGES__; ['en'] without), DEFAULT_LANGUAGE,
                    IS_LOCALIZED, codeOf, languageOf(code) (configured or default), languageLabel(code) (endonym via
                    Intl.DisplayNames, capitalized), LANG_COOKIE 'cms_lang', langFromCookie, rememberLanguage (1 year,
                    SameSite=Lax, not HttpOnly). No vue-i18n: shared plain data
i18n/index.ts       createCmsI18n(lang): vue-i18n (legacy false), public messages of every language (eager), fallback en;
                    loadAdminMessages(i18n, code) (lazy admin chunk + en, once per code); messages are deep-copied per
                    instance (vue-i18n broke on shared JSON module objects); LANGUAGE_SWITCH inject key +
                    useLanguageSwitch() ({current, languages, change}); useFormat() (date/dateTime/time/number in the
                    current tag); translate()/setStoreTranslate (stores: createAdmin registers the admin i18n)
i18n/siteConfigLabels.ts  useConfiguredLabel(key, fallback) (message if it exists and its English text still equals
                    the configured label), useConfigLabels (siteConfig.fields/groups/entries), useBlockLabels
                    (blocks.<name>.title/description)
locales/public/<code>.json   common.*, site.*, screens.*, account.*, siteConfig.* (public app + admin)
locales/admin/<code>.json    admin.*, editor.*, blocks.* (admin only); en/de/fr/it, same keys (i18n.test.ts checks)
components/LanguagePicker.vue    presentational language menu (nb-ui Dropdown, `placement="auto"`: opens above without room
                    below): trigger = nb-ui `Flag` + short code (EN, DE; `globe` for a language without a flag), rows =
                    flag + endonym (`lang` = tag) + check; optional `counts` badges; trigger only during SSR/hydration
                    (Popover isn't SSR-safe). Used by LanguageDropdown and the editor's EditLanguageSelect
components/LanguageDropdown.vue  LanguagePicker over useLanguageSwitch() (nothing when < 2 languages); admin: AppNav
                    bottom row between User and log out (`align="end"`: the sidebar scrolls), AdminLogin;
                    public: SiteFooter bottom row (localized sites)
components/UnsavedDialog.vue     translated copy of nb-ui's UnsavedChangesDialog (Menus, Configuration)
```
- Admin (`createAdmin`): language = `cms_lang` cookie else default, `<html lang>` set at runtime, switch = load the
  admin chunk, set locale + cookie (no reload). Public app (`createPublicApp`): language = `state.lang` (from the URL
  prefix, never the cookie); switch = cookie + `switchLanguage` from `entry-client.ts` (no page load, see there); a
  full page load of `localePath(current router path, code)` without it.
- Writing UI text: `$t('key')` in templates, `const { t } = useI18n()` in setup, `<I18nT keypath scope="global">`
  for sentences with links/markup, `t(key, n)` with `'{n} one | {n} many'` plurals. Add the key to all four
  `locales/*/<code>.json` (Vitest fails otherwise); German is Swiss: "ss", never "ß". Keep English wording stable:
  e2e selects by text. `@`, `|`, `{`, `}` are message syntax (`{'@'}`). Dates/numbers via `useFormat()`.
- nb-ui texts without props stay English (aria "Close"); pass `label`/`cancelText`/`confirmText` where props exist
  (`Loading`, `Dropdown`, `TagInput`, `ProgressBar`, every `confirm()`).
- Not translated: API error messages (ProblemDetails `detail`, English), `api-error.ts`/`backupUpload.ts` client errors.

## Public site (`src/public/`, `server/`)
```
state.ts            PublicState {slug, page|null, menu, config, baseUrl, lang (code)}: what a page or screen renders
                    from (API RenderState mirrors it)
locale.ts           language prefixes (default unprefixed): localeBase(code) = router history base (`/de/`),
                    localePath('/about', 'de') = `/de/about` ('/' → `/de`), stripLocale(pathname) → {code, path},
                    localeHref(url, code) (link blocks: site paths prefixed, external/admin/API/already prefixed kept).
                    Router links get the prefix from the history base; this is for code building URLs itself
createPublicApp.ts  app factory (SSR app or client app), seeds stores (publicPage, menus, siteConfig); returns app, router, pinia
PublicApp.vue       .site-theme root: AdminBar (client-only) + SiteHeader + RouterView + SiteFooter
PublicPage.vue      route component of pages: PageView (template or PageContent) of the store's page, else NotFound
templates.ts        getTemplate(name): the instance's src/templates/*.vue; TemplatePage / TemplateProps types
router.ts           history base = language prefix (see locale.ts); routes `home` / `/home` redirect / customer screens (lazy) / `public-page` / catch-all; first
                    navigation keeps the server state (a screen path the server answered with not-found, i.e. public
                    auth off → catch-all, same URL), later ones load the page first; unknown page, admin route or
                    deeper path → full page load; leaving a page for login/register adds `?redirect=`
screens/            customer screens: index.ts SCREENS (slug, route name, title, account) + screenOf(state) + RouteMeta
                    `screen`; Login, Register, ForgotPassword, ResetPassword, VerifyEmail (guest), UserProfile,
                    UserPassword (account: `/user/*`)
session.ts          useGuestScreen (target() after login, carry(path) keeps redirect, signed-in → straight on),
                    useCustomerSession (account pages: ready once ensureSession() confirms a customer, else → login)
redirect.ts         safeRedirect: same-site paths only, no guest screen, no /admin (+ redirect.test.ts)
store.ts            usePublicPageStore: current slug + page + lang, load(slug) (`?lang=`)
head.ts             <head> tags (canonical in the page's language; localized: hreflang per language + x-default,
                    og:locale + alternates): title via documentTitle ("{metaTitle} - {firmName}", firm name alone without a meta title;
                    no firm name → meta title, then page title), description (metaDescription, else the first
                    paragraph), canonical, OG/Twitter (og:title = meta or page title, og:site_name = firm, og:image =
                    first image, else config.shareImage), icon; serializeState
entry-server.ts     render(state) → {head, html, state, tag}: page, screen (screenOf) or not-found, memory history with the
                    language base (`vite build --ssr` → dist-ssr/); `tag` → `<html lang>` (splice)
entry-client.ts     hydrates from #__STATE__ (web history base = its language prefix); without it (shell) strips the
                    prefix and fetches page + menu + config in one `GET /api/public/state/{slug}?lang=` (screens/deeper
                    paths: no slug), adds head tags; interceptors (401 →
                    login with redirect); after mounting, a `data-refresh` on #__STATE__ (stored HTML older than a
                    menu/config change, set by the API during the quiet re-render) → refresh.ts. Language switch
                    (`switchLanguage(path)`, passed to createPublicApp): fetch that language's state, pushState, client-
                    render a new app under its prefix, unmount the old one + destroy its history in the same task, set
                    `<html lang>` + title; not-found → full load; latest switch wins. A popstate listener registered
                    before vue-router's remounts on back/forward into another language (stopImmediatePropagation)
refresh.ts          refreshSurroundings(pinia): one `GET /api/public/state?lang=` (no slug) → seeds menus.main + the site
                    config store, keeps both on failure (not awaited, no hydration mismatch)
render.test.ts      Vitest: render output, escaping, screens, languages (vitest.config.ts: vue + nbUi only; two projects:
                    `localized` = en-US + de/fr/it like the playground, `single-language` = `*.single.test.ts`)
server/render-server.js  renderer service: POST /render, GET /version (hash of template + SSR bundle), /health; reads
                         dist/public.html + dist-ssr/entry-server.js from the working directory (= instance app root)
server/template.js       splice(): fills public.html placeholders (function replacers: content may contain `$&`) + sets
                         `<html lang>` to the render's tag; types in .d.ts
server/site-lang.js      build time, part of cms(): parseConfig (JSONC), site.lang → language tags (validated like the
                         API: tags, unique codes) → <html lang> (default) + __CMS_LANGUAGES__
server/theme.js          build time, part of cms(): theme.colors (validated like the API) → themeStyle <style> + palette
server/dev-ssr.js        part of cms(): dev server-renders published pages + customer screens via the app's
                         /src/entry-server.ts (one `/api/public/state[/{slug}]?lang=` fetch, screens also `/api/public/instance`
                         for publicAuth; falls through to the SPA when neither a page nor a screen with public auth on);
                         language prefixes + `cms_lang` redirect like the API; inlines the app's compiled `src/style.css`
                         as `<style data-vite-dev-id>` (styled before the JS; Vite's client adopts it, HMR works)
server/screens.js        SCREEN_SLUGS (dev SSR; mirrors screens/index.ts and the API's PublicScreens)
```
- Public code imports **directly** (`../lib/web-editor/view/ViewBlockList.vue`, `../stores/media`), type-only from the
  barrel: the barrel pulls the editor into the public bundle.
- Shared components (SiteHeader/Footer, PageContent, block views, screens) are server-rendered: no `window`/`document`
  in setup. Screens render the same HTML for everyone: query (`redirect`, `token`) and session only after mount.
- Check hydration in the browser console (dev logs mismatches) after changing them.

## Styling rules
- Tailwind for everything; arbitrary values (`text-[#abc]`) when needed.
- Allowed exceptions (nothing else):
  - `@apply` component classes in `src/lib/web-editor/web-editor.css` and in SFC `<style>` blocks
    (add `@reference "<relative>/style.css";` at the top of the style block)
  - inline `:style` only for **user-chosen block colours** (block wrappers)
- `src/style.css` (exported as `@trainpaths/cms/style.css`; the instance's stylesheet imports it): Tailwind import,
  nb-ui `theme.css` import (tokens), `@source` of the package, `cms-*` chrome tokens, v3-compat base styles. The
  package entries import no CSS: the instance stubs import the instance stylesheet.
- Write Tailwind **v4** class names (`outline-hidden`, `shadow-xs`, `rounded-sm`, ...).

## Theme (`@trainpaths/nb-ui/theme.css`, imported by src/style.css) — web-editor look, app-wide
Tokens come from the nb-ui package; override one with an `@theme { ... }` in `src/style.css` after the import.
```css
--spacing: 1px           /* ALL spacing utilities are px: p-4 → 4px, m-16 → 16px, w-240 → 240px */
--font-sans: 'Roboto Variable', -apple-system, BlinkMacSystemFont, sans-serif  /* self-hosted by nb-ui (fontsource), CSP font-src 'self' ok */
--color-primary: #6a428a      --color-primary-dark: #54356e
--color-secondary: #4a4d50    --color-accent: #86bbbd     --color-accent-dark: #3a7679
--color-danger/error: #d32f2f --color-success: #16a34a   --color-warning: #eab308
--color-white: #f4f5f6        --color-black: #131b23
--nb-rounded: 1              /* 0 = sharp corners app-wide (scales --radius-*; bare `rounded` ignores it) */
```
`accent` is light: nb-ui `Button` defaults to an accent fill (black text); accent-coloured **text** uses `accent-dark`.
Use `bg="primary"` for the main call-to-action (e.g. "+ New Page").
Usage: `bg-primary hover:bg-primary-dark`, `text-danger`, plus the normal Tailwind palette (gray-*, red-*...).
nb-ui's `theme.css` also safelists the colour classes its components build at runtime (`bg-${name}`) and `@source`s them.
Base layer restores v3 defaults: gray-200 border colour, gray-400 placeholders, pointer cursor on buttons.

## API client (auto-generated, committed)
- Output: `src/api/` (`*.gen.ts`, **never edit**, committed so instances need no hey-api), config `openapi-ts.config.ts`.
  Regenerate: `pnpm gen-api` (root) from the committed `openapi/swagger.json`; CI fails when it drifts.
- The contract is rewritten by every backend build (`pnpm build:back` at repo root); commit it, then `pnpm gen-api`.
- Base URL `''` → same-origin `/api/*` (Vite proxy in dev → `VITE_API_BASE_URL`; nginx in the container).
- Bearer token added by `src/interceptors.ts` (memory-only token from the auth store).
- **Call the SDK directly** (`const { data } = await getApiPages()`), no wrapper layer. `openapi-ts.config.ts`
  sets `throwOnError: true`: `data` is non-optional and failures throw `ApiError(message, status)` (`src/api-error.ts`,
  built by its error interceptor `toApiError`, registered by the admin and the public client, from ProblemDetails
  `detail ?? title`; status 0 = network).
  `errorMessage(err, fallback)` for UI text.
- Types are exact (required/nullable follow C# nullability, enums are string unions), so `lib/web-editor/core/types.ts`
  re-exports them. Backend DTO change → `pnpm build:back` → `pnpm gen-api` → vue-tsc shows every affected caller.

## Router (`src/router/index.ts`) — admin SPA, staff only
Customer screens are routes of the public app (`src/public/router.ts`, see Public site).
| Path | View | Access |
|------|------|--------|
| `/admin/login` | `admin/AdminLogin.vue` ("Admin Login" for staff; Back → `/`, no register/forgot links) | guest |
| `/` | `home`: hand-off to the public site (full page load; `toPublicSite` guard; first navigation → not-found) | public |
| `/admin` | `Dashboard.vue` (welcome + "Log Out" button (`dashboard-logout`), "Pages" card → `/admin/pages` (`dashboard-pages`); super admins: `components/backups/BackupsSummary.vue` = schedule (`lib/backupSchedule.ts` scheduleSummary), next/last run, retention, saved count + automatic + total size, latest, free space, last error, "Manage backups" → `/admin/profile?tab=backups`; name `dashboard`) | staff |
| `/admin/profile` | `Profile.vue`: super admins get tabs (`?tab=backups`) **Profile** (`components/ProfileDetails.vue`) + **Backups** (`components/backups/BackupsPanel.vue`: schedule off/daily/weekly/every 2 weeks/monthly (UTC, local time hint;
weekday for weekly + biweekly, day 1-28 for monthly), storage folder (`hostPath`, else "Developer has not set backup folder"), back up now, upload with progress (`lib/backupUpload.ts`, XHR), list rows: Restore (primary) / download + delete icons (download = blob; restore = confirm → logout → admin login)); others just ProfileDetails | staff |
| `/admin/change-password` | `ChangePassword.vue` (wraps `components/ChangePasswordForm.vue`, shared with the customer screen) | staff |
| `/admin/pages` | `admin/Pages.vue` (list with search / tag filter / status filter (All statuses / Published / Not published; only here, `v-model:status` shows it) / updatedAt sort toggle (clock + arrow icon button, down = newest first) (`PageFilters` + `composables/usePageFilter.ts`), tag chips, row = view/preview link + "…" `components/ActionMenu.vue` (default label "Actions"; Edit → modal `components/PageEditDialog.vue` (slug, published (switching on publishes the saved copy), tags; one PUT with only what changed, all or nothing), Duplicate (`POST /api/pages/{id}/duplicate`: "<title> (copy)", slug `-2`…, stays on the list), Export (JSON of the saved page), Delete (confirm; disabled for locked pages)); "+ New Page" opens a row: Import (primary outline, upload icon + "Import", icon only below sm; file → `parsePageExport` → `POST /api/pages/import` → editor of the new draft, toast warns about missing images), title input, Create, cancel (x icon)) | staff |
| `/admin/pages/:id` | `admin/PageEditor.vue` → `WebEditor` (no nav) | staff |
| `/admin/pages/:id/preview` | `admin/PagePreview.vue` (draft preview, no nav; localized: language select (`?lang=`, the editor eye passes its language), the working copy localized client-side via `core/localize.ts`) | staff |
| `/admin/media` | `admin/Media.vue` (rename + alt; library grid 3/4/6 cols; details in a fixed right sidebar on lg+, modal below; click again to close) | staff |
| `/admin/menus` | `admin/Menus.vue` (menu list by handle, all menus with items from one `GET /api/menus` (switching applies from the list, no fetch; save replaces the entry) (new menu = handle only, normalized, immutable), tree editor `components/menus/` (item = label + optional link: typing a page's path `/slug` links that page by id, else a `/path` or http(s) URL; drag & drop of rows and of picker pages via `useMenuDrag` (module-level state): 10px `MenuDropSlot` lines between rows (no layout shift, like BlockList), row middle = nest inside (bottom shade); ↑↓⇤⇥ buttons; ops in `lib/menus/tree.ts`), published-page picker with `PageFilters` (click + or drag); explicit Save; switching menus / leaving with changes → `UnsavedChangesDialog`) | staff |
| `/admin/configuration` | `admin/Configuration.vue` (logo, icon, share image (og:image fallback); site config shaped by `useInstanceStore().config.siteConfig`: fixed fields form, one section per group with `components/config/ConfigEntryRow.vue` rows (preset = fixed name/type, custom = name + type select, address = 4 inputs; reorder, remove unless required), "+ <preset>" / "+ Custom entry" buttons; duplicate names block saving; logo + icon via `ConfigImageField`; leave-guard when dirty) | staff |
| `/:slug` | `public-page`: hand-off like `home` (links in SiteHeader/Footer, Pages list) | public |
| `/:pathMatch(.*)*` | `NotFound.vue` | public |

**Nav:** `App.vue` renders `components/AppNav.vue` on `meta.requiresAuth` routes unless `meta.hideNav` (editor,
preview): md+ fixed left sidebar (`w-220`; content gets `md:pl-220`): `<favicon> CMS`, items with nb-ui `Icon`s
(Dashboard, Pages, Menus, Media, Configuration), bottom row `User` → `/admin/profile` + LanguageDropdown (localized) +
exit icon button (log out).
Below md: sticky 56px top bar + burger → the same `#app-nav-menu` as a full-screen overlay below the bar (body scroll
locked). `/admin/login` has no nav. Fixed overlays: md+ the sidebar is on the left (no top offset).
**Admin bar** (`components/AdminBar.vue`, public site): thin dark bar above `SiteHeader` for staff → `/admin`, log out
(auth store `logout()`, refresh cookie). Client-only: shown after mount from the `auth_type` hint (`readAuthType()`),
no API call, so SSR/hydration are identical for everyone (an expired session lands on the admin login).

Guard (`router.beforeEach`): `meta.requiresAuth` → `ensureSession()` else `/admin/login` (remembers
`redirectAfterLogin`); `meta.requiresStaff` (every `/admin*` route) → `authType === 'staff'` else a full page load to
`/` (customer session; a guard redirect to `home` would 404 on the first navigation); `meta.guest` → signed-in staff go
to `dashboard`.
**New top-level route?** Also add its first path segment to `ReservedSlugs` in
`api-backend/Services/Pages/PageService.cs` so no page can claim it.

`PageContent.vue` renders title + `ViewBlockList` and seeds the media store from `page.media`; shared by
the public site (`src/public/PublicApp.vue`) and `PagePreview.vue`. Both also render `components/SiteHeader.vue` (nav partial: logo → home + the
`main` menu from `usePublicMenusStore`, seeded on a fresh install with Home `/` + Login `/login` (`MenuService.EnsureMainAsync`; `site/SiteNavLink.vue` shows the `/login` item as "Account" → `/user/profile` for a signed-in customer, client-only); md+ row with hover/focus dropdowns, deeper levels indented inside; burger panel
below md; pieces in `components/site/`) and `components/SiteFooter.vue` (logo + `config.fields.firmName` + filled
`config.groups.contact` entries: address lines, mailto/tel/link by type; bottom row = `config.footerLinks`, the
published config pages marked `footer`, + `LanguageDropdown` on a localized site). Hidden only when all are empty. Other groups: instance overrides/templates.
`src/lib/siteConfig.ts`: pure helpers for reading the config (firmName, configEntry, filledEntries, hasValue,
addressLines, entryHref, entryText, emptyAddress) and `components/SiteConfigEntry.vue` (one entry's value: address
lines / mailto-tel-link / text; footer + templates), exported via `@trainpaths/cms/site`.

**Favicon:** `public/favicon.svg` (`CMS_ICON`) is the admin icon. Public pages get the site icon from the site config in
their server-rendered `<head>` (`headTags()` in `src/public/head.ts`, falls back to `CMS_ICON`; the fallback shell inserts the same tags client-side).

## Pinia stores
- `src/stores/auth.ts` — `user`, `accessToken` (memory only), `authType`, `isAuthenticated`, `loading`,
  `error`, `redirectAfterLogin` (admin). Refresh token is an HttpOnly cookie; `main.ts` awaits `ensureSession()`
  before mounting; the public app only on screens, after mount (`public/session.ts`). SSR-safe (storage reads guarded). Actions: `login`, `register`, `registerStaff`, `logout`, `fetchCurrentUser`,
  `refreshAuth` (single-flight), `ensureSession`, `changePassword`, `updateProfile`, email flows.
  localStorage holds `auth_type` only.
- `src/stores/editor.ts` — `useEditorStore` (block tree, history, page state, saving).
- `src/stores/pages.ts` — `usePagesStore`: stale-while-revalidate cache. `items`/`loaded`/`load()` (list renders
  cached, refreshes in the background), detail cache `cached(id)` (returns a copy) / `remember` (full details: GET,
  create, import, duplicate) / `applySaved(summary, patch?)` (a PUT's `PageSummary` → list row; summary fields minus
  `blockCount` + the sent fields merged into a cached detail) / `prefetch` (row focus, or ~150 ms pointer rest: hover
  intent, sweeping fetches nothing) / `add` / `forget`. The editor store shows the cached detail first, then applies
  the server copy only if nothing was edited meanwhile. Vitest `stores/pages.test.ts`.
- Editor saving (`stores/editor.ts`): every write is `PUT /api/pages/{id}`. Saves write the working copy only (never
  live, ARCHITECTURE.md → Draft vs live); `isDirty` = title/meta/blocks JSON ≠ last saved (`savedContent`), `savePage`
  skips clean pages; `publish()` / `unpublish()` send `{status}` alone (after a save in flight; `publishing` flag);
  `pageHasUnpublishedChanges` from every PUT/GET; `updateSlug` / `updateTags` send `{slug}` / `{tags}`.
- `src/stores/siteConfig.ts` — `useSiteConfigStore`: public site `config`, `load()` once (single-flight, never throws),
  `refresh()` (refetch, keeps the current value on failure; `data-refresh` pages), `set()` after the admin form saves.
- `src/stores/instance.ts` — `useInstanceStore`: `config` = `InstanceConfig` (publicAuth, excludedBlocks, siteConfig,
  machineTranslation) +
  `isOffered(blockName)` (not in excludedBlocks), from
  `/api/public/instance`, `load()` once in `createAdmin()` (failure keeps the API defaults: auth off, nothing hidden).
- `src/stores/tags.ts` — `useTagsStore`: tags in use + page counts (`names`, `popular` = top 3); `load()` once per
  session (again after `invalidate()`); the pages list `refresh()`es after its tag-changing actions (filter shows the
  counts), the editor only `invalidate()`s.
- `src/stores/menus.ts` — `usePublicMenusStore`: public menus by handle, `load(handle)` once per session (never throws),
  `refresh(handle)` (refetch, keeps the current entry on failure; `data-refresh` pages), `invalidate()` after the admin
  saves a menu. `data-refresh` pages go through `public/refresh.ts` (one state call) instead. Vitest `stores/refresh.test.ts`.
- `src/stores/media.ts` — `useMediaStore`: library `items` (`load` once per session (the picker), `load(true)` = the
  Media view's explicit refresh, `upload`, `update` (alt + optional fileName), `remove`) and a
  `byId` cache that image blocks render from (seeded from page responses via `seed`).

## Token refresh (`src/interceptors.ts`)
`setupInterceptors(onUnauthenticated)`, installed by both apps. On 401 (except login/register/refresh/logout):
single-flight `refreshAuth()`, replay the request; on failure clear auth and call `onUnauthenticated` (admin →
`/admin/login`, public → `/login?redirect=<current>`). Logout / password change: the component emits, its parent
navigates (`ProfileDetails` `logged-out` + `passwordTo` prop, `ChangePasswordForm` `changed` + `backTo`).

## UI library (`@trainpaths/nb-ui`, separate public repo github.com/trainpaths/nb-ui)
Installed from a git tag (`"@trainpaths/nb-ui": "github:trainpaths/nb-ui#vX.Y.Z"`; pnpm fetches the codeload tarball, no
git needed in Docker). Ships raw `.vue`/`.ts`: `nbUi()` (inside `cms()`) excludes it from dep pre-bundling and
enables overrides. A **peer dependency** of this package (spec = the exact git tag; the playground and every instance
depend on it directly: pnpm 12 refuses git-hosted packages as sub-dependencies, `blockExoticSubdeps`). Inside the
package `import { Button, useToast } from '@trainpaths/nb-ui'`; instance code may use `@trainpaths/cms/ui`.
Bumping nb-ui = devDependency + peerDependency here + `playground/package.json` (instances follow on upgrade).
`Button`, `Input`, `FormField`, `Form`, `Card`, `DescriptionList`, `DescriptionItem`, `Alert`, `Link`,
`Loading` (spinner + label, `fullscreen`; used wherever data is fetched; `index.html` has a static copy as boot loader),
`Toast`, `ToastContainer`, `useToast()`, `UnsavedChangesDialog` + `useUnsavedChanges(dirty, save)` (`confirmLeave()`
→ Save / Don't save / Cancel; use in `onBeforeRouteLeave`; Menus + Configuration), `TagInput` (single-word chips; props
`suggestions`/`popular`/`max`/`maxLength`/`normalize`), `Modal` (`PageEditDialog`), `Select`, `Switch`, `Badge`,
`EmptyState`, `useConfirm()` (every delete; never `window.confirm`; e2e clicks `confirm-ok`). Also available: `Textarea`,
`Checkbox`, `RadioGroup`, `Dropdown`, `Tooltip`, `Tabs`, `Table`, `Pagination`, `Avatar`, `Skeleton`, `ProgressBar`, `Icon`.
Colour props (`bg`, `text`, `border`) take theme colour names. `App.vue` mounts the global `ToastContainer` + `ConfirmDialog`.
- UI icons: nb-ui `Icon`, no inline SVGs (exceptions: block-type `…Icon.vue`s, the `PageFilters` sort icon). Directional
  shapes exist once pointing left (`chevron`, `arrow`, `panel`) + `rotate` (90 up, 180 right, 270 down). Missing
  icon → add it to nb-ui's `Icon.vue`.
- `Button` renders `type="button"` by default: submit buttons need `type="submit"` (outside the form: `form="<id>"`).
- **Override** a component (per instance): create `src/overrides/ui/<Name>.vue` in the app (restart dev server). It
  replaces the library's file everywhere, incl. inside the library. Wrap the original via
  `import Base from '@trainpaths/nb-ui/src/<Name>.vue'`.
- **Change the library itself**: edit in `../nb-ui` (playground: `pnpm dev` there), try it here with
  `pnpm --filter @trainpaths/cms link ../../nb-ui`, then tag a release and bump the tag in this `package.json`.

## E2E tests
Live in the playground instance: `playground/e2e/` → see `playground/CLAUDE.md`.

## Code style (.prettierrc)
Tabs (width 4), no semicolons, single quotes, 120-char lines, one attribute per line in templates.
Prettier isn't a dependency; format with `pnpm dlx prettier@3 --write <paths>`.

## Scripts (`pnpm --filter @trainpaths/cms <script>`, or the root scripts)
```
gen-api           # regenerate src/api/ from openapi/swagger.json (commit the result)
typecheck         # vue-tsc -b: src (tsconfig.app.json) + vite-plugin.js/server/*.js (tsconfig.node.json, checkJs)
test              # Vitest (src/ only)
```
Dev server, build and e2e run in `playground/` (root `pnpm dev`, `pnpm build:front`, `pnpm test:e2e`).

## Key files
```
src/main.ts            createAdmin(): admin SPA bootstrap
src/public/            public site (SSR + hydration), see above; the instance's public.html is its template
server/                render service + dev SSR (JS)
vite-plugin.js         cms(): the instance Vite setup + overrides
src/router/index.ts    admin routes + guards
src/style.css          Tailwind import + nb-ui theme.css + @source of the package + v3-compat base (+ web-editor.css)
src/lib/web-editor/     block editor library (own CLAUDE.md)
src/views/             route views (admin/ = page management)
src/components/        app shell pieces (AppNav, AdminBar, SiteHeader + site/, SiteFooter, ConfigImageField, PageFilters,
                       PageEditDialog, ActionMenu ("…" menu like nb-ui Dropdown + per-item title/testid; nb-ui icon names),
                       menus/ tree editor, ProfileDetails + ChangePasswordForm (staff + customer), backups/BackupsPanel +
                       BackupsSummary (dashboard))
src/composables/       usePageFilter (search/tag/status/sort over PageSummary[])
src/lib/menus/tree.ts  pure menu tree ops (locate, move, indent/outdent, depth checks)
src/lib/pageExport.ts  page export file (`{format: "cms-page", version, exportedAt, page}`): toPageExport, parsePageExport
                       (throws PageExportError = user-facing message) + Vitest; editor PageSettings "Export" downloads the
                       on-screen state, the row menu's Export the saved copy
src/lib/download.ts    saveBlob / saveJson (browser file download)
src/lib/backupSchedule.ts  interval/weekday labels + scheduleSummary (panel + dashboard)
src/lib/backupUpload.ts uploadBackup (XHR for progress; ensureSession + bearer itself) + formatBytes
nginx.conf             `/` + `/{slug}` + `/user/{name}` (optionally prefixed `/de/…`, never `/api/`) → API HTML endpoint (X-Accel-Redirect to index.html/public.html), SPA fallback, /api proxy (64k; /api/pages 2m; /api/menus 512k; /api/media 11m; /api/backups 2g, unbuffered, 15 min timeouts; proxy headers set once at server level; real client IP from the host proxy's X-Forwarded-For, trusted from 172.16.0.0/12), CSP (img-src 'self' only: uploads are same-origin)
public/favicon.svg     CMS admin icon (emitted by cms() unless the instance has its own)
```
