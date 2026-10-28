# Localization: overview and handoff

Tracking issue: trainpaths/cms#45. Phase 1 shipped in PR trainpaths/cms#46. Phases 2–4 are written up here so each one
can be picked up by a separate agent as its own PR. **Read this file first, then your phase's file:**

| Phase | File | Depends on |
|-------|------|------------|
| 2. Per-language public site (URLs, rendering, language switch) | `PHASE_2_PUBLIC_LANGUAGES.md` | #46 merged |
| 3. Translated page content (blocks, title, meta) | `PHASE_3_CONTENT_TRANSLATION.md` | Phase 2 merged |
| 4. DeepL pre-fill of missing translations | `PHASE_4_DEEPL.md` | Phase 3 merged |

Design rationale lives in `claude-context/ARCHITECTURE.md` → Localization; this folder is the work plan. When a phase
lands, move its decisions into ARCHITECTURE.md (present tense), tick its boxes in #45 and update the phase file's
status line.

## Settled decisions (agreed with the owner; don't reopen them)
- **Config**: `cms.config.json` `"site": { "lang": "de-CH" }` (one language) or
  `{ "default": "en-US", "localization": ["de-CH", "fr-CH", "it-CH"] }`. Empty/missing `localization` = single-language
  site, which must behave exactly as before (no prefixes, no switch, no translation UI).
- **Code vs tag**: a language's **code** is its primary subtag (`de-CH` → `de`), unique per site. The code is the URL
  prefix, the `cms_lang` cookie value, the message file name and the key of stored translations (so changing `de-CH` to
  `de-DE` keeps content). The **tag** goes into `<html lang>`, `hreflang`, `og:locale` and `Intl` formatting.
- **Scope**: UI strings (done) and page content (title, meta title/description, block text). Menu labels, site config
  values, media alt text and slugs stay single-language (follow-ups, not in these phases).
- **Public URLs**: the code as a path prefix, default language unprefixed: `/about`, `/de/about`, `/fr/about`. One
  stored HTML per page **and language**. Rejected: one URL chosen by cookie/`Accept-Language` (crawlers and link
  previews only ever see one language) and client-side text swapping (wrong-language flash, no SEO, hydration
  mismatches). Storage cost (a few KB per page and language) was accepted.
- **Content model**: per-field translations on **one shared block tree**: structure, images, colours and widths are
  shared; translatable attributes get a value per language; missing ones fall back to the default language.
- **One `cms_lang` cookie** for admin and public site, written only by `<LanguageDropdown />` (visiting a `/fr/…` link
  does not change it). No per-user DB setting.
- **Machine translation**: DeepL fills *missing* fields on request; the owner reviews; nothing publishes by itself.

## State after Phase 1 (what you build on)
Frontend (`frontend/`):
```
server/site-lang.js        siteLanguages(json) → tags, default first (validated: tag pattern, unique codes, no unknown
                           keys); readSiteLanguages(root); codeOf(tag); setHtmlLang(html, tag). cms() writes the default
                           tag into <html lang> of index.html/public.html and defines __CMS_LANGUAGES__ (all tags)
src/i18n/languages.ts      LANGUAGES [{code, tag}] (default first), DEFAULT_LANGUAGE, IS_LOCALIZED, codeOf, languageOf(code)
                           (configured or default), languageLabel(code) (endonym), LANG_COOKIE, langFromCookie(cookieHeader),
                           rememberLanguage(code). Plain data, no vue-i18n: safe anywhere (server, router, stores)
src/i18n/index.ts          createCmsI18n(lang) (one per app / per server render), loadAdminMessages(i18n, code),
                           LANGUAGE_SWITCH + useLanguageSwitch() ({ current, languages, change(code) }), useFormat()
                           (dates/numbers in the current tag), translate()/setStoreTranslate (Pinia stores, admin only)
src/i18n/siteConfigLabels.ts  useConfiguredLabel / useConfigLabels / useBlockLabels (config-provided English names → messages)
src/locales/public/*.json  strings of the public app (also loaded by the admin); src/locales/admin/*.json admin + editor
src/components/LanguageDropdown.vue  nb-ui Dropdown over useLanguageSwitch(); nothing when < 2 languages; the server
                           render shows only the trigger (nb-ui Popover touches `document` in setup)
src/main.ts                admin: language = cms_lang cookie else default; switch = swap strings in place + cookie
src/public/createPublicApp.ts  public app in state.lang; switch = cookie + full page load (Phase 2)
src/public/locale.ts       localeBase / localePath / stripLocale (Phase 2); router history base = language prefix
src/public/head.ts         headTags(state, t) (canonical per language, hreflang, og:locale), documentTitle(...)
```
Phase 2 (API): `rendered_pages` keyed `(PageId, Lang)`, `RenderState.Lang`, `RenderWorker` renders every language,
`PublicHtmlController` prefixed routes + `cms_lang` redirect + `Vary: Cookie`, `?lang=` on `/api/public/pages/{slug}` and
`/api/public/state` (`PageService.GetPublishedBySlugAsync(slug, lang)` ignores it until Phase 3), `CmsSiteLang.Codes /
DefaultCode / IsPrefix / Resolve`, language codes reserved as slugs.
Phase 3: `Block.translations` + `Page.Translations/PublishedTranslations`, `PageService.Localize` / `LiveTitle`,
`GetPublishedByIdsAsync(ids, codes)`, `PublicCache` per code, `?lang=` on public menus/site-config; frontend
`translatable` attributes, `store.editLang` / `pageTranslations` / `updateBlockTranslation` / `setPageText`,
`useBlockAttribute` per language + `useBlockText` (was `useAttributeFallback`), `usePageText`, `core/translatable.ts` (`textFields`: the
list Phase 4 sends to DeepL), `core/localize.ts`, `EditLanguageSelect.vue`, page export v2.
Phase 4: `POST /api/translate` (`TranslateController`, `ITranslator` → `DeepLTranslator`, `TranslationOptions` from
`DEEPL_API_KEY`), `InstanceConfig.machineTranslation`, editor `useMachineTranslation` + `store.applyTranslations`.
API (`api-backend/`): `Services/Cms/CmsConfig.cs` `CmsSiteLang { Default, Localization, Tags, IsLocalized, CodeOf(tag) }`
(read from a string or the object by `CmsSiteLangConverter`), `CmsConfigLoader.ValidateLang` (tags, unique codes, no
code equal to a `PageService.ReservedSlugs` entry or a configured page slug). The API doesn't use the languages for
anything else yet. Playground config: en-US default + de-CH, it-CH, fr-CH.

## Conventions every phase follows
- Branch off fresh `main` after the previous phase merged (`git fetch origin --prune && git checkout main && git pull`).
  One PR per phase into `main`, `Closes` nothing (#45 stays open until Phase 4), body links #45. Conventional Commits
  (`feat(api)`, `feat(editor)`, `feat(db)`...). Mark instance-breaking changes with `!` (none are expected: everything
  is additive and single-language sites must not change).
- UI text: message keys only, added to **all four** files of the right area (`locales/public` for anything the public
  app renders, `locales/admin` for admin/editor). `src/i18n/i18n.test.ts` fails on missing keys. German is Swiss ("ss",
  never "ß"). Never reword existing English strings (e2e selects by text). vue-i18n syntax: `{name}`, `one | many`
  plurals with `t(key, n)`, `{'@'}` for a literal `@`, `<I18nT scope="global">` for sentences containing links.
- Single-language sites are the regression to guard: every phase adds a test that a config without `localization`
  renders/behaves exactly as before.
- Contract changes: `pnpm build:back` regenerates `frontend/openapi/swagger.json`, then `pnpm gen-api`; commit both
  (CI checks drift). EF migrations: add new ones only (`dotnet ef migrations add <Name>`), never edit `Init`.
- Docs to keep current (repo CLAUDE.md rule): `frontend/docs/INSTANCE_GUIDE.md` for anything instances see,
  `frontend/CLAUDE.md`, `frontend/src/lib/web-editor/CLAUDE.md`, `api-backend/CLAUDE.md`, `claude-context/*.md`.

## Working in a cloud session (what tripped Phase 1)
- `codeload.github.com` may be blocked, so `pnpm install` can't fetch nb-ui. Workaround: `git clone --depth 1 --branch
  v1.2.0 https://github.com/trainpaths/nb-ui.git <scratch>/nb-ui`, put an **uncommitted** `.pnpmfile.cjs` at the repo
  root whose `readPackage` hook rewrites `@trainpaths/nb-ui` to `file:<scratch>/nb-ui`, run
  `pnpm install --no-frozen-lockfile`, delete the pnpmfile and `git checkout pnpm-lock.yaml`. Adding a dependency:
  `pnpm --filter @trainpaths/cms add <pkg> --lockfile-only` without the pnpmfile, then put the nb-ui lockfile entries
  back to the `https://codeload.github.com/...tar.gz/<sha>` form if pnpm rewrote them as `git+https`.
- pnpm reinstalls before every script (and fails the same way): call tools directly, e.g.
  `frontend/node_modules/.bin/vue-tsc -b`, `frontend/node_modules/.bin/vitest run`, `node_modules/.bin/eslint .`,
  `playground/node_modules/.bin/vite build`.
- No .NET SDK and no Docker: C# then compiles first in CI. Keep C# changes small per commit, mirror existing patterns
  closely, and say plainly in the PR which parts are untested. With Docker available use the SDK-image recipe in the
  root CLAUDE.md.
- Playwright: the pinned version wants a browser that isn't installed; use a throwaway config with
  `launchOptions: { executablePath: '/opt/pw-browsers/chromium' }` and no `globalSetup`. Pages that need the API can't
  be tested without the stack (the admin login works against `vite` alone).

## Known limits carried over (not part of Phases 2–4 unless noted)
- vue-i18n (with its message compiler) + the public messages add ~24 KB gzipped to the public JS. Fix later: precompile
  messages in `cms()` (`@intlify/unplugin-vue-i18n` or `@intlify/message-compiler`) and alias the runtime-only build.
- nb-ui: built-in texts are English (aria "Close"; `UnsavedChangesDialog` is replaced by `components/UnsavedDialog.vue`),
  `Popover` isn't SSR-safe. Needs an nb-ui release (label props or a message hook; SSR-safe watch).
- API error messages (ProblemDetails `detail`) and a few client-side errors (`api-error.ts`, `lib/backupUpload.ts`)
  stay English. Transactional emails are English.
