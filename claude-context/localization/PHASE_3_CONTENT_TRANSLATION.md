# Phase 3: translated page content

Status: **implemented** (PR pending, stacked on Phase 2, see #45). Read `README.md` (this folder) first. Issue trainpaths/cms#45. Started after Phase 2
(it relies on per-language rendering and `?lang=` on the public endpoints).

## Goal
The owner switches the editor to German and types the German text into the same blocks; title, meta title and meta
description get a German version too. `/de/about` shows the German text, falling back to the default language for
anything untranslated. Structure (which blocks, their order), images, colours, widths, alignment stay shared by all
languages. Single-language sites: no change (no language switch in the editor, `translations` never written).

## Out of scope
Machine translation (Phase 4), per-language slugs, per-language menus/site config/media alt, a different block
structure per language.

## Design

### Block contract (frontend ⇄ API ⇄ DB)
```json
{ "id": "p1", "name": "paragraph", "attributes": { "text": "Hello", "alignment": "left" },
  "translations": { "de": { "text": "Hallo" }, "fr": { "text": "Bonjour" } }, "innerBlocks": [] }
```
- `Models/Pages/Block.cs`: `Dictionary<string, Dictionary<string, JsonElement>>? Translations` (omitted from JSON when
  null/empty: `[JsonIgnore(Condition = WhenWritingNull)]` and normalize empty → null). Same type for the `Blocks` jsonb
  value converter (System.Text.Json Web defaults, camelCase) → no migration for blocks (jsonb).
- `PageService.ValidateBlocks`: language keys match `^[a-z]{2,3}$`, values primitive (same rule as attributes), at most
  one entry per configured-or-not code (unknown codes are **kept**: removing a language from the config and adding it
  back restores its text; public reads ignore them). Counts against the existing 2 MB request limit, nothing new.
- Frontend `BlockInstance` (`src/lib/web-editor/core/types.ts`, generated `Block` type): optional
  `translations?: Record<string, Record<string, string | number | boolean>>`. `createBlockInstance` doesn't set it;
  `deepCloneBlock`/duplicate copies it; export/import keeps it.
- **Translatable attributes are opt-in** per block definition: `AttributeDefinition` gets `translatable?: boolean`.
  Built-ins: heading `text`, paragraph `text`, list-item `text`, card `title` + `description`, image `caption`, link
  `label` + `url` (a link may point to a language-specific page). Everything else (`mediaId`, `level`, `alignment`,
  `ordered`, `width`, colours, `blockWidth`) stays shared. Instance blocks opt in the same way.

### Page-level text
- `Page`: `Translations` + `PublishedTranslations` (jsonb, `Dictionary<string, PageTextTranslation>` with
  `record PageTextTranslation(string Title, string MetaTitle, string MetaDescription)`; empty strings = untranslated),
  value converter + `ValueComparer` like `Blocks`. Migration `PageTranslations` (two nullable jsonb columns, default
  `'{}'`).
- `PageDetail`, `UpdatePageRequest` (partial: `translations` replaces the whole map when sent), `ImportPageRequest`,
  duplicate: carry `Translations`. Length limits per field = the existing title/meta limits.
- Publish copies `Translations` → `PublishedTranslations` with the blocks; `MatchesSnapshot` also compares the
  translations (serialize both, like blocks) so `HasUnpublishedChanges` sees translation-only edits. Unpublish keeps them.
- Page export (`frontend/src/lib/pageExport.ts`): `PAGE_EXPORT_VERSION = 2` adds `translations`; still reads version 1
  (no translations). Block translations ride inside `blocks` either way.

### Public reads (API resolves, views stay unchanged)
- `PageService.Localize(LivePage page, string code, string defaultCode)` (pure, static, unit-tested): returns blocks
  where each block's `attributes` are overlaid with `translations[code]` (only non-empty strings / any non-string
  primitive), `translations` removed from the output; `title`/`metaTitle`/`metaDescription` from
  `PublishedTranslations[code]` when non-empty, else the default. Default code (or unknown) → the default content with
  `translations` stripped (never ship other languages' text to visitors).
- Use it in `GetPublishedBySlugAsync(slug, code)` and `GetPublishedByIdsAsync(ids, code)` (render worker calls it per
  language, from Phase 2's loop). `PublicPagesController`/`PublicStateController` pass the Phase 2 `?lang=`.
- Titles shown around pages: `SiteConfigService.FooterLinksAsync` and `MenuService.GetPublicAsync` (page links without
  a label show the page title) take the language and use the translated published title; `PublicCache` keys its
  entries by language (`(handle, code)`, site config per code). `RenderedPage.Title` stores the default-language title
  (the widen check compares that one); a translated title change must also trigger `Refresh()` (menus/footers of
  other pages): extend `PageUpdate.LiveRenamed` to "live title in any language changed".
- `PublicPage` DTO doesn't change shape (it's already the localized content).

### Editor (`src/stores/editor.ts`, `src/lib/web-editor/`)
- Store: `editLang` (code; default = `DEFAULT_LANGUAGE.code`, reset on `loadPage`), `pageTranslations`
  (`Record<code, {title, metaTitle, metaDescription}>`), `updateBlockTranslation(id, code, patch)` (same history +
  `trackChange` as `updateBlockAttributes`; empty string deletes the key, empty object deletes the language),
  `contentJson()` includes `pageTranslations` (dirty tracking, undo). `savePage` sends `translations` when changed.
- `composables/useBlockAttribute.ts`: look up the block type (`getBlockType(block.name)`); when the attribute is
  `translatable` and `store.editLang` isn't the default: get = `translations[editLang][key]` (empty → `''`), set =
  `updateBlockTranslation`. Return a second ref or expose `placeholder` = the default-language value so inputs can show
  it greyed (`:placeholder="fallback || $t('…')"`). Non-translatable attributes are unaffected. Built-in Edit components
  that bypass the composable (`HeadingEdit.vue`, `ListItemEdit.vue` use their own computed over `updateBlockAttributes`)
  must switch to `useBlockAttribute`.
- `EditorHeader.vue`: language select (nb-ui `Select` or a small dropdown) bound to `store.editLang`, only when
  `IS_LOCALIZED`; shows "DE · 3 untranslated" style badge. `PageSettings.vue`: title/meta fields edit
  `pageTranslations[editLang]` in a non-default language (default values as placeholders); a "Translations" section
  lists each language with its count of missing translatable fields (walk the tree: translatable attributes with a
  default value but no translation, plus title/meta).
- Non-default language editing: a one-line hint above the canvas ("Layout, images and colours apply to all
  languages"); block add/move/delete stay allowed (shared tree).
- `PagePreview.vue`: language select; renders through `core/localize.ts` (TS mirror of `PageService.Localize`, same
  rules, Vitest) so the preview shows the saved working copy in that language.
- The public app never sees `translations` (API strips them); `ViewBlockList` and all `*View.vue` stay untouched.

## Tasks
1. API: `Block.Translations` + validation, `Page.Translations/PublishedTranslations` + migration, DTOs, publish/snapshot,
   `Localize` + its callers, menu/footer titles per language + cache keys, import/duplicate. `pnpm build:back` +
   `pnpm gen-api`.
2. Frontend: types, `translatable` on built-ins, store + `useBlockAttribute`, migrate HeadingEdit/ListItemEdit to the
   composable, header language select, PageSettings translations, preview + `core/localize.ts`, page export v2,
   messages (admin, 4 languages).
3. Tests + docs.

## Tests
- xUnit unit (`PageServiceTests`): validation of `translations` (bad code, object value → 400), `Localize` overlay +
  fallback + stripping, `MatchesSnapshot` with translation-only change, import of v1 and v2 payloads, duplicate copies
  translations.
- xUnit integration: `PUT` with translations → `PageDetail` round trip; publish → `GET /api/public/pages/about?lang=de`
  German, `?lang=fr` default, no `translations` key in the JSON; render worker states per language carry the localized
  page; footer link titles per language.
- Vitest: `core/localize.ts`; `useBlockAttribute` read/write per language (Pinia + editor store); `pageExport` v1/v2;
  `i18n.test.ts` keeps the 4 message files in sync.
- e2e `editor/translations.spec.ts`: switch editor to Deutsch, type a German paragraph + title, publish, open
  `/de/<slug>` → German text and title, `/fr/<slug>` → default text; undo of a translation edit; status shows
  "unpublished changes" after a translation-only edit.

## Docs to update
`claude-context/BLOCK_SYSTEM.md` (contract with `translations`, `translatable` in the add-a-block checklist),
`claude-context/ARCHITECTURE.md` (Block contract, Draft vs live, Localization), `frontend/src/lib/web-editor/CLAUDE.md`,
`api-backend/CLAUDE.md` (DTOs, validation, tables), `frontend/docs/INSTANCE_GUIDE.md` (instance blocks: mark
`translatable`; templates read localized `page`), this file's status line, #45 checkboxes.
