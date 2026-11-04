# playground — the in-repo CMS instance

A minimal instance app of `@trainpaths/cms` (`workspace:*` → `../frontend`), shaped like a client site repo
(the `cms-starter` template): CMS development, the compose stack and the e2e suite run against it.
Package internals: `frontend/CLAUDE.md`. Why instances: `claude-context/ARCHITECTURE.md` → Framework and instances.

```
cms.config.json     instance config (API; `site.lang` + `theme.colors` also read by cms() at build time; compose.yaml mounts it at /app/cms.config.json in the api container,
                    restart the api after editing): publicAuth on (e2e uses customer accounts), site.lang en-US + localization de-CH/it-CH/fr-CH, theme.colors (primary, accent, dark, light,
                    white: block-fixes.spec picks them), site config field
                    `vatId` + group `socials` (instagram/linkedin/facebook, no custom entries), the default pages
                    + `imprint` (template page)
index.html          admin SPA shell (boot loader) → src/main.ts
public.html         public site template: <!--app-head-->, <!--app-html-->, <!--app-state--> placeholders (filled by the
                    renderer / dev SSR) → src/entry-client.ts
src/style.css       @import '@trainpaths/cms/style.css' + site theme (`--color-brand` token for the callout block, .site-theme
                    primary-dark; primary comes from cms.config.json theme.colors)
src/main.ts         ./style.css + createAdmin()      from @trainpaths/cms/admin
src/entry-client.ts ./style.css + hydratePublic()    from @trainpaths/cms/public/client
src/entry-server.ts export { render }      from @trainpaths/cms/public/server (vite build --ssr → dist-ssr/)
vite.config.ts      plugins: [cms()], envDir '../' (repo-root .env, shared with docker compose)
tsconfig.app.json   extends @trainpaths/cms/tsconfig.json (vue-tsc also checks the package source it imports)
src/blocks/callout/ instance block example (Edit imports @trainpaths/cms/editor, View @trainpaths/cms/site; `text` translatable)
src/overrides/NotFound.vue   override example: wraps the package's NotFound
src/templates/imprint.vue    template page example: PageContent + `after` slot: firm name, contact, VAT ID, socials
e2e/                Playwright suite (below); e2e/tests/instance/ covers the examples above
demo/, demo.html, vite.demo.config.ts   browser-only demo for GitHub Pages (below)
```
Extension points in general: `frontend/CLAUDE.md` → Package surface.

Commands (repo root): `pnpm dev` (API stack in docker + Vite dev server with dev SSR), `pnpm build:front`
(`vue-tsc -b && vite build && vite build --ssr`), `pnpm test:e2e`. The Dockerfile's `frontend-build` stage builds this
app; the `renderer` and `frontend` images serve its `dist/` and `dist-ssr/`.

## E2E tests (`e2e/`, Playwright, Chromium, 1 worker)
Needs the full stack (`pnpm docker:up`).
```
e2e/playwright.config.ts       base URL from PLAYWRIGHT_BASE_URL (default http://localhost:5173); reducedMotion 'reduce'
                               (transitions off: layout specs measure right after toggling sidebars)
e2e/global-setup.ts            frontend-reachable preflight
e2e/fixtures/auth.fixture.ts   authenticatedPage (registers a fresh customer)
e2e/fixtures/test-utils.ts     uniqueEmail, testPassword, registerCustomer / loginCustomer / logoutCustomer (public-site
                               screens: register + login land on `/` or the redirect, logout via `/user/profile`)
e2e/fixtures/staff.fixture.ts  staffPage (logs in as bootstrap super admin) + createPage/insertBlock/uploadImage helpers,
                               publish(page) (saves if dirty, then header Publish) / unpublish(page),
                               editLanguage(page, 'Deutsch') (editor header language menu),
                               rowAction(row, 'page-edit'|'page-duplicate'|'page-export'|'page-delete') (opens the row's "…" menu)
e2e/tests/smoke.spec.ts, e2e/tests/nav.spec.ts, e2e/tests/auth/*.spec.ts, e2e/tests/site/{public-site,server-render}.spec.ts
(needs the seeded default pages)
e2e/tests/editor/{pages,pages-view,blocks,block-fixes,editor-ux,outline,publish,responsive}.spec.ts, e2e/tests/media/media.spec.ts,
e2e/tests/config/configuration.spec.ts, e2e/tests/pages/{tags,export-import}.spec.ts, e2e/tests/menus/menus.spec.ts (replaces the main menu)
e2e/tests/instance/instance.spec.ts   callout block (SSR), NotFound override, imprint template (locked)
e2e/tests/backups/backups.spec.ts     serial: schedule + back up + download, restore (signs out; later pages gone), no tab without
                                      super_admin (registers an editor via the API). The restore re-renders every public page.
e2e/tests/site/meta.spec.ts           meta title/description + firm name → rendered <head> (clears the firm name again)
e2e/tests/editor/translations.spec.ts  German paragraph + title in the editor → /de/<slug>, /fr falls back, undo, unpublished changes
e2e/tests/editor/deepl.spec.ts        "Translate missing fields" with /api/public/instance + /api/translate stubbed (page.route), undo
e2e/tests/site/languages.spec.ts      footer language switch → /de/… without a reload (+ back/forward), cms_lang redirect, German screens, per-language HTML
                                      (the only spec that sets cms_lang; its own browser context)
```
Public auth off / `blocks.exclude` / `editable: false` aren't in the playground config: covered by the API tests
(`InstanceConfigTests`); check the UI by editing `cms.config.json` + `docker compose restart api`.
The full suite exceeds the default rate limits (per IP): run the stack with raised limits, e.g.
`RATE_LIMIT_PERMIT=10000 RATE_LIMIT_GENERAL_PERMIT=10000 RATE_LIMIT_PAGES_PERMIT=10000 docker compose up -d --build`
(shell env overrides `.env`; CI does the same via `.env`).
Thousands of leftover e2e pages make the pages list slow enough to fail timing-sensitive specs; CI starts fresh. To get a
fresh stack next to the dev one: `FRONTEND_PORT=5373 API_PORT=5383 BACKUP_DIR=/tmp/cmse2e-backups docker compose -p cmse2e
up -d --build` (own BACKUP_DIR: the default ./backups is shared), `PLAYWRIGHT_BASE_URL` pointing at it, then
`docker compose -p cmse2e down -v` (only that project's volumes).
After changing frontend code, rebuild `frontend` **and** `renderer` together: the renderer's public.html must reference the
same asset hashes, else public pages render unstyled and never hydrate.
Editor specs need staff credentials in the Playwright env — the same values as the stack's
`BOOTSTRAP_SUPERADMIN_*` (or `E2E_STAFF_EMAIL/PASSWORD`); without them they are skipped:
```bash
BOOTSTRAP_SUPERADMIN_EMAIL=... BOOTSTRAP_SUPERADMIN_PASSWORD=... pnpm test:e2e
```
First run on a machine: `pnpm --filter cms-playground exec playwright install chromium`.

## Demo (`demo/`, GitHub Pages)
The real editor and public view of **one page**, with no server: the generated client's `fetch` is replaced by an
in-browser mock of the API (`client.setConfig({ fetch })`), and the page lives in localStorage. Nothing in the package
changes for it; it uses deep imports (`@trainpaths/cms/src/….ts`, with extension: the `./src/*` export adds none).
`pnpm dev:demo` (no Docker), `pnpm build:demo` → `dist-demo/` (deployed by `.github/workflows/pages.yml`),
`pnpm test:demo`.
```
demo.html             shell → demo/main.ts
vite.demo.config.ts   cms() without dev SSR + plugin `cms-demo`: build input demo.html only (emitted as index.html),
                      `/` → demo.html in dev, the package's MediaUploadButton.vue → demo/NoUpload.vue (no uploads);
                      base './' (Pages serves /cms/)
demo/main.ts          app: Pinia, CMS i18n (+ demo/messages.ts after each loadAdminMessages), mock fetch, toApiError,
                      hash router: `#/` + `#/:slug` = ViewPage, `#/edit` = EditPage, `#/de/home` (editor view link) →
                      switch language + `#/`; one language for view + UI (`LANGUAGE_SWITCH`, demo/context.ts)
demo/mockApi.ts       createMockFetch(store, {languages}): page GET/PUT (draft/live like the API: publish copies the
                      working copy to the live snapshot, hasUnpublishedChanges = differs from it, slug fixed: page is
                      `locked`), public state/page/menu/site-config (localized via core/localize.ts), media list +
                      tags (read-only), instance config from cms.config.json; anything else → 403 "Not available in the demo"
demo/store.ts         localStorage `cms-demo:v1` (working copy + live snapshot; memory when storage fails), reset()
demo/seed.ts          the page (every block incl. callout; de complete, fr/it partly: shows the untranslated badges),
                      main menu (labels per language), site config (contact only: no firm name or logo)
demo/media.ts, images/  seeded placeholder SVGs (the picker's library); media URLs are always served fresh
                      (stored ones would point at an older deploy's hashed assets)
demo/ViewPage.vue     SiteHeader + PageView + SiteFooter of the published page; delegated clicks on link-block hrefs
                      (`/`, `/edit`, other paths → "only one page" toast)
demo/EditPage.vue     the package's Editor.vue (view link + back → `#/`)
demo/DemoMenu.vue     floating round button (cog + "Demo", bottom left, z-45) → Popover: edit/view, reset (confirm →
                      reload), GitHub links
demo/mockApi.test.ts  Vitest (demo/vitest.config.ts): draft vs live, storage + reset, unpublish, languages, refusals
```
