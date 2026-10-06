# playground — the in-repo CMS instance

A minimal instance app of `@trainpaths/cms` (`workspace:*` → `../frontend`), shaped like a client site repo
(the `cms-starter` template): CMS development, the docker-compose stack and the e2e suite run against it.
Package internals: `frontend/CLAUDE.md`. Why instances: `claude-context/ARCHITECTURE.md` → Framework and instances.

```
cms.config.json     instance config (API; `site.lang` also read by cms() at build time; docker-compose mounts it at /app/cms.config.json in the api container,
                    restart the api after editing): publicAuth on (e2e uses customer accounts), site.lang en, site config field
                    `vatId` + group `socials` (instagram/linkedin/facebook, no custom entries), the default pages
                    + `imprint` (template page)
index.html          admin SPA shell (boot loader) → src/main.ts
public.html         public site template: <!--app-head-->, <!--app-html-->, <!--app-state--> placeholders (filled by the
                    renderer / dev SSR) → src/entry-client.ts
src/style.css       @import '@trainpaths/cms/style.css' + site theme (`--color-brand` token, .site-theme primary = brand)
src/main.ts         ./style.css + createAdmin()      from @trainpaths/cms/admin
src/entry-client.ts ./style.css + hydratePublic()    from @trainpaths/cms/public/client
src/entry-server.ts export { render }      from @trainpaths/cms/public/server (vite build --ssr → dist-ssr/)
vite.config.ts      plugins: [cms()], envDir '../' (repo-root .env, shared with docker compose)
tsconfig.app.json   extends @trainpaths/cms/tsconfig.json (vue-tsc also checks the package source it imports)
src/blocks/callout/ instance block example (Edit imports @trainpaths/cms/editor, View @trainpaths/cms/site)
src/overrides/NotFound.vue   override example: wraps the package's NotFound
src/templates/imprint.vue    template page example: PageContent + `after` slot: firm name, contact, VAT ID, socials
e2e/                Playwright suite (below); e2e/tests/instance/ covers the examples above
```
Extension points in general: `frontend/CLAUDE.md` → Package surface.

Commands (repo root): `pnpm dev` (API stack in docker + Vite dev server with dev SSR), `pnpm build:front`
(`vue-tsc -b && vite build && vite build --ssr`), `pnpm test:e2e`. The Dockerfile's `frontend-build` stage builds this
app; the `renderer` and `frontend` images serve its `dist/` and `dist-ssr/`.

## E2E tests (`e2e/`, Playwright, Chromium, 1 worker)
Needs the full stack (`pnpm docker:up`).
```
e2e/playwright.config.ts       base URL from PLAYWRIGHT_BASE_URL (default http://localhost:5173)
e2e/global-setup.ts            frontend-reachable preflight
e2e/fixtures/auth.fixture.ts   authenticatedPage (registers a fresh customer)
e2e/fixtures/staff.fixture.ts  staffPage (logs in as bootstrap super admin) + createPage/insertBlock/uploadImage helpers,
                               rowAction(row, 'page-edit'|'page-duplicate'|'page-export'|'page-delete') (opens the row's "…" menu)
e2e/tests/smoke.spec.ts, e2e/tests/nav.spec.ts, e2e/tests/auth/*.spec.ts, e2e/tests/site/{public-site,server-render}.spec.ts
(needs the seeded default pages)
e2e/tests/editor/{pages,pages-view,blocks,block-fixes,editor-ux,outline,publish,responsive}.spec.ts, e2e/tests/media/media.spec.ts,
e2e/tests/config/configuration.spec.ts, e2e/tests/pages/{tags,export-import}.spec.ts, e2e/tests/menus/menus.spec.ts (replaces the main menu)
e2e/tests/instance/instance.spec.ts   callout block (SSR), NotFound override, imprint template (locked)
e2e/tests/backups/backups.spec.ts     serial: schedule + back up + download, restore (signs out; later pages gone), no tab without
                                      super_admin (registers an editor via the API). The restore re-renders every public page.
e2e/tests/site/meta.spec.ts           meta title/description + firm name → rendered <head> (clears the firm name again)
```
Public auth off / `blocks.exclude` / `editable: false` aren't in the playground config: covered by the API tests
(`InstanceConfigTests`); check the UI by editing `cms.config.json` + `docker compose restart api`.
The full suite exceeds the default rate limits (per IP): run the stack with raised limits, e.g.
`RATE_LIMIT_PERMIT=10000 RATE_LIMIT_GENERAL_PERMIT=10000 RATE_LIMIT_PAGES_PERMIT=10000 docker compose up -d --build`
(shell env overrides `.env`; CI does the same via `.env`).
Thousands of leftover e2e pages make the pages list slow enough to fail timing-sensitive specs; CI starts fresh. To get a
fresh stack next to the dev one: `docker compose -p cmse2e -f docker-compose.yml -f <override setting other container_name
values>` with other `FRONTEND_PORT`/`API_PORT`, `PLAYWRIGHT_BASE_URL` pointing at it, then `down -v` (only that project's volumes).
After changing frontend code, rebuild `frontend` **and** `renderer` together: the renderer's public.html must reference the
same asset hashes, else public pages render unstyled and never hydrate.
Editor specs need staff credentials in the Playwright env — the same values as the stack's
`BOOTSTRAP_SUPERADMIN_*` (or `E2E_STAFF_EMAIL/PASSWORD`); without them they are skipped:
```bash
BOOTSTRAP_SUPERADMIN_EMAIL=... BOOTSTRAP_SUPERADMIN_PASSWORD=... pnpm test:e2e
```
First run on a machine: `pnpm --filter cms-playground exec playwright install chromium`.
