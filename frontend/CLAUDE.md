# frontend — the `@trainpaths/cms` package

Vue 3 + TypeScript + Tailwind v4 + Vite + hey-api + vue-router + Pinia.
The block editor lives in `src/lib/web-editor/` → see its `CLAUDE.md`.
**Two apps:** the admin SPA (`createAdmin()` in `src/main.ts`) and the server-rendered public site (`src/public/`).
Why/how: `claude-context/ARCHITECTURE.md` → Public pages.

**Instance developer guide:** `docs/INSTANCE_GUIDE.md` (in `files`, read from node_modules by instance repos) — update it
with every change to `cms.config.json`, exports, extension/override points or the image setup.

**A package, not an app:** shipped as raw source (no build step, like nb-ui), installed by instance apps from a git
tag (`github:trainpaths/cms#vX.Y.Z&path:/frontend`); in this repo `playground/` uses it via `workspace:*`. The
instance owns `index.html`, `public.html`, `vite.config.ts` (`plugins: [cms()]`), `src/style.css` (imports
`@trainpaths/cms/style.css` + its site theme), `cms.config.json` (read by the API; `cms()` reads only `site.lang`, at
build time) and three stubs that import the stylesheet and call the package entries. Why: `claude-context/ARCHITECTURE.md` → Framework and instances.

## Package surface (`package.json` exports)
```
./admin            src/main.ts                createAdmin(): Pinia, API base URL, session restore, router, mount #app
./public/client    src/public/entry-client.ts hydratePublic(): hydrate #__STATE__ or render the fallback shell
./public/server    src/public/entry-server.ts render(state) for the SSR bundle (instance: src/entry-server.ts)
./editor           src/editor.ts              block authoring, edit side (defineBlock, useBlockAttribute, InlineToolbar,
                                              ToolbarDropdown, SettingsSection, BlockList, Media*, useEditorStore, types)
./site             src/site.ts                public side for views/templates/overrides, no editor code (ViewBlockList,
                                              PageContent, useMediaImage, site config/menu stores, site config helpers
                                              from lib/siteConfig.ts + SiteConfigEntry, TemplateProps, types)
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
HTML files from `cms.config.json` `site.lang` (`server/site-lang.js`, read per transform so dev picks up edits).
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
- Instance config (`cms.config.json`): only the API reads it at runtime (`site.lang`: `cms()` sets `<html lang>` of
  index.html + public.html at build time, `server/site-lang.js`, invalid tag fails the build); the admin loads `/api/public/instance`
  (`stores/instance.ts`, in `createAdmin()`): `publicAuth` (router: routes with `meta.publicAuth` → not-found when off)
  and `excludedBlocks` (`isOffered(name)`, used by `Inserter.vue` / `BlockInserterSidebar.vue`). Locked/template/editable page flags
  come with `PageSummary`/`PageDetail` (slug + delete disabled, non-editable → notice instead of the canvas).

## Public site (`src/public/`, `server/`)
```
state.ts            PublicState {slug, page|null, menu, config, baseUrl}: what a page renders from (API RenderState mirrors it)
createPublicApp.ts  app factory (SSR app or client app), seeds stores (publicPage, menus, siteConfig)
PublicApp.vue       .site-theme root: SiteHeader + PageView (template or PageContent; or NotFound) + SiteFooter
templates.ts        getTemplate(name): the instance's src/templates/*.vue; TemplatePage / TemplateProps types
router.ts           routes `home` / `/home` redirect / `public-page` / catch-all; first navigation keeps the server
                    state, later ones load the page first; unknown page, app route or deeper path → full page load
store.ts            usePublicPageStore: current slug + page, load(slug)
head.ts             <head> tags: title via documentTitle ("{metaTitle} - {firmName}", firm name alone without a meta title;
                    no firm name → meta title, then page title), description (metaDescription, else the first
                    paragraph), canonical, OG/Twitter (og:title = meta or page title, og:site_name = firm, og:image =
                    first image, else config.shareImage), icon; serializeState
entry-server.ts     render(state) → {head, html, state}   (built with `vite build --ssr` → dist-ssr/)
entry-client.ts     hydrates from #__STATE__; without it (shell) fetches page/menu/config, adds head tags
render.test.ts      Vitest: render output, escaping (vitest.config.ts: vue + nbUi only)
server/render-server.js  renderer service: POST /render, GET /version (hash of template + SSR bundle), /health; reads
                         dist/public.html + dist-ssr/entry-server.js from the working directory (= instance app root)
server/template.js       splice(): fills public.html placeholders (function replacers: content may contain `$&`); types in .d.ts
server/site-lang.js      build time, part of cms(): site.lang of cms.config.json (JSONC, validated) → <html lang>
server/dev-ssr.js        part of cms(): dev server-renders published pages via the app's /src/entry-server.ts
                         (falls through to the SPA otherwise)
```
- Public code imports **directly** (`../lib/web-editor/view/ViewBlockList.vue`, `../stores/media`), type-only from the
  barrel: the barrel pulls the editor into the public bundle.
- Shared components (SiteHeader/Footer, PageContent, block views) are server-rendered: no `window`/`document` in setup.
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
  built by the error interceptor in `src/interceptors.ts` from ProblemDetails `detail ?? title`; status 0 = network).
  `errorMessage(err, fallback)` for UI text.
- Types are exact (required/nullable follow C# nullability, enums are string unions), so `lib/web-editor/core/types.ts`
  re-exports them. Backend DTO change → `pnpm build:back` → `pnpm gen-api` → vue-tsc shows every affected caller.

## Router (`src/router/index.ts`)
| Path | View | Access |
|------|------|--------|
| `/login` | `Login.vue` (customer login; like register / forgot / reset / verify: `meta.publicAuth`, not-found when the instance has public auth off) | guest |
| `/admin/login` | `admin/AdminLogin.vue` ("Admin Login" for staff; Back → `/`, no register/forgot links) | guest |
| `/register` | `Register.vue` (customer signup) | guest |
| `/` | `home`: hand-off to the public site (full page load; `toPublicSite` guard; first navigation → not-found) | public |
| `/admin` | `Dashboard.vue` (welcome + profile links; name `dashboard`) | auth |
| `/profile` | `Profile.vue` | auth |
| `/change-password` | `ChangePassword.vue` | auth |
| `/forgot-password` | `ForgotPassword.vue` | guest |
| `/reset-password` | `ResetPassword.vue` (`?token=`) | public |
| `/verify-email` | `VerifyEmail.vue` (`?token=`) | public |
| `/admin/pages` | `admin/Pages.vue` (list with search / tag filter / updatedAt sort toggle (clock + arrow icon button, down = newest first) (`PageFilters` + `composables/usePageFilter.ts`), tag chips, edit modal `components/PageEditDialog.vue` (slug, published, tags; saves only what changed via slug PUT → publish/unpublish → tags PUT), create, view/preview, delete) | staff |
| `/admin/pages/:id` | `admin/PageEditor.vue` → `WebEditor` (no nav) | staff |
| `/admin/pages/:id/preview` | `admin/PagePreview.vue` (draft preview, no nav) | staff |
| `/admin/media` | `admin/Media.vue` (rename + alt; library grid 3/4/6 cols; details in a fixed right sidebar on lg+, modal below; click again to close) | staff |
| `/admin/menus` | `admin/Menus.vue` (menu list by handle (new menu = handle only, normalized, immutable), tree editor `components/menus/` (item = label + optional link: typing a page's path `/slug` links that page by id, else a `/path` or http(s) URL; drag & drop of rows and of picker pages via `useMenuDrag` (module-level state): 10px `MenuDropSlot` lines between rows (no layout shift, like BlockList), row middle = nest inside (bottom shade); ↑↓⇤⇥ buttons; ops in `lib/menus/tree.ts`), published-page picker with `PageFilters` (click + or drag); explicit Save; switching menus / leaving with changes → `UnsavedChangesDialog`) | staff |
| `/admin/configuration` | `admin/Configuration.vue` (logo, icon, share image (og:image fallback); site config shaped by `useInstanceStore().config.siteConfig`: fixed fields form, one section per group with `components/config/ConfigEntryRow.vue` rows (preset = fixed name/type, custom = name + type select, address = 4 inputs; reorder, remove unless required), "+ <preset>" / "+ Custom entry" buttons; duplicate names block saving; logo + icon via `ConfigImageField`; leave-guard when dirty) | staff |
| `/:slug` | `public-page`: hand-off like `home` (links in SiteHeader/Footer, Pages list) | public |
| `/:pathMatch(.*)*` | `NotFound.vue` | public |

**Nav:** `App.vue` renders `components/AppNav.vue` (sticky top bar: Dashboard, Pages, Menus, Media, Configuration, User → `/profile`;
staff-only items hidden for customers; burger menu below md) on `meta.requiresAuth` routes unless `meta.hideNav`
(editor, preview). Customer auth screens (`/login`, `/register`, `/forgot-password`, `meta.publicNav`) render
`components/PublicNav.vue` (loads menu + config → `SiteHeader`) instead; `/admin/login` and the other auth screens have no nav. Fixed overlays must start below its 56px (`top-56`).

Guard (`router.beforeEach`): `meta.requiresAuth` → `ensureSession()` else `/admin/login` for `/admin*` paths,
`/login` otherwise (remembers `redirectAfterLogin`); `meta.requiresStaff` → `authType === 'staff'` else `dashboard`; `meta.guest` → authenticated users go to `dashboard` (`/admin`).
**New top-level route?** Also add its first path segment to `ReservedSlugs` in
`api-backend/Services/Pages/PageService.cs` so no page can claim it.

`PageContent.vue` renders title + `ViewBlockList` and seeds the media store from `page.media`; shared by
the public site (`src/public/PublicApp.vue`) and `PagePreview.vue`. Both also render `components/SiteHeader.vue` (nav partial: logo → home + the
`main` menu from `usePublicMenusStore`, seeded on a fresh install with Home `/` + Login `/login` (`MenuService.EnsureMainAsync`); md+ row with hover/focus dropdowns, deeper levels indented inside; burger panel
below md; pieces in `components/site/`) and `components/SiteFooter.vue` (logo + `config.fields.firmName` + filled
`config.groups.contact` entries: address lines, mailto/tel/link by type; bottom row = `config.footerLinks`, the
published config pages marked `footer`). Hidden only when all are empty. Other groups: instance overrides/templates.
`src/lib/siteConfig.ts`: pure helpers for reading the config (firmName, configEntry, filledEntries, hasValue,
addressLines, entryHref, entryText, emptyAddress) and `components/SiteConfigEntry.vue` (one entry's value: address
lines / mailto-tel-link / text; footer + templates), exported via `@trainpaths/cms/site`.

**Favicon:** `public/favicon.svg` (`CMS_ICON`) is the admin icon. Public pages get the site icon from the site config in
their server-rendered `<head>` (`headTags()` in `src/public/head.ts`, falls back to `CMS_ICON`; the fallback shell inserts the same tags client-side).

## Pinia stores
- `src/stores/auth.ts` — `user`, `accessToken` (memory only), `authType`, `isAuthenticated`, `loading`,
  `error`, `redirectAfterLogin`. Refresh token is an HttpOnly cookie; `main.ts` awaits `ensureSession()`
  before mounting. Actions: `login`, `register`, `registerStaff`, `logout`, `fetchCurrentUser`,
  `refreshAuth` (single-flight), `ensureSession`, `changePassword`, `updateProfile`, email flows.
  localStorage holds `auth_type` only.
- `src/stores/editor.ts` — `useEditorStore` (block tree, history, page state, saving).
- `src/stores/pages.ts` — `usePagesStore`: stale-while-revalidate cache. `items`/`loaded`/`load()` (list renders
  cached, refreshes in the background), detail cache `cached(id)` (returns a copy) / `remember` / `prefetch`
  (on page-row hover/focus) / `add` / `forget`. The editor store shows the cached detail first, then applies
  the server copy only if nothing was edited meanwhile, and remembers every save/publish/slug response.
- `src/stores/siteConfig.ts` — `useSiteConfigStore`: public site `config`, `load()` once (single-flight, never throws),
  `set()` after the admin form saves.
- `src/stores/instance.ts` — `useInstanceStore`: `config` = `InstanceConfig` (publicAuth, excludedBlocks, siteConfig) +
  `isOffered(blockName)` (not in excludedBlocks), from
  `/api/public/instance`, `load()` once in `createAdmin()` (failure keeps the API defaults: auth off, nothing hidden).
- `src/stores/tags.ts` — `useTagsStore`: tags in use + page counts (`names`, `popular` = top 3); `load()` once per
  session, `refresh()` after every tag write.
- `src/stores/menus.ts` — `usePublicMenusStore`: public menus by handle, `load(handle)` once per session (never throws),
  `invalidate()` after the admin saves a menu.
- `src/stores/media.ts` — `useMediaStore`: library `items` (`load`, `upload`, `update` (alt + optional fileName), `remove`) and a
  `byId` cache that image blocks render from (seeded from page responses via `seed`).

## Token refresh (`src/interceptors.ts`)
On 401 (except login/register/refresh/logout): single-flight `refreshAuth()`, replay the request;
on failure clear auth and go to `loginRouteFor(authType)` (router: staff → `/admin/login`, else `/login`;
also used after logout / password change).

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
src/router/index.ts    routes + guards
src/style.css          Tailwind import + nb-ui theme.css + @source of the package + v3-compat base (+ web-editor.css)
src/lib/web-editor/     block editor library (own CLAUDE.md)
src/views/             route views (admin/ = page management)
src/components/        app shell pieces (AppNav, SiteHeader + site/, SiteFooter, ConfigImageField, PageFilters,
                       PageEditDialog, menus/ tree editor)
src/composables/       usePageFilter (search/tag/sort over PageSummary[])
src/lib/menus/tree.ts  pure menu tree ops (locate, move, indent/outdent, depth checks)
nginx.conf             `/` + `/{slug}` → API HTML endpoint (X-Accel-Redirect to index.html/public.html), SPA fallback, /api proxy (64k; /api/pages 2m; /api/menus 512k; /api/media 11m), CSP (img-src 'self' only: uploads are same-origin)
public/favicon.svg     CMS admin icon (emitted by cms() unless the instance has its own)
```
