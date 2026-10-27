# Phase 4: DeepL pre-fill of missing translations

Status: **implemented** (PR pending, stacked on Phase 3, see #45). Read `README.md` (this folder) first. Issue trainpaths/cms#45. Started after Phase 3
(it fills the Phase 3 `translations` through the editor store).

## Goal
In the editor, for a non-default language, "Translate missing fields" sends the default-language text of every
untranslated translatable field (block attributes + page title/meta) to DeepL and fills the results in as **one undo
step**. The owner reviews and publishes as usual; nothing is saved or published automatically beyond the normal
autosave. Off unless the site has a DeepL key.

## Out of scope
Translating UI strings (they ship as message files), menus/site config, other providers (keep the `ITranslator`
seam so one could be added), automatic translation on publish.

## Design

### Configuration
- `.env` `DEEPL_API_KEY` → compose `Translation__DeepL__ApiKey` → `Translation:DeepL:ApiKey` (`TranslationOptions`,
  bound like `RendererOptions`). Empty/missing = feature off. Add to `.env.example` (commented, empty), `compose.yaml`
  (api service env), root `CLAUDE.md` env table, `api-backend/CLAUDE.md` config keys.
- `InstanceConfig` DTO (`Models/Dto/Instance.cs`) gains `bool MachineTranslation` (key present); the admin's
  `useInstanceStore` default `false`. Never expose the key.

### API (`Services/Translation/`, `Controllers/TranslateController.cs`)
- `ITranslator { Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string sourceTag, string targetTag, CancellationToken ct) }`
  → `DeepLTranslator` as a typed `HttpClient` (registered like `IRendererClient` in `Program.cs`):
  - Base URL: key ending in `:fx` → `https://api-free.deepl.com`, else `https://api.deepl.com`. Header
    `Authorization: DeepL-Auth-Key <key>`. `POST /v2/translate` JSON `{ text: [...], source_lang, target_lang }`,
    batches of ≤ 50 texts (DeepL limit), order preserved; skip empty strings (return them as is, no characters spent).
  - Language mapping: source = default code upper-case (`EN`, `DE`...; DeepL source languages have no variants).
    Target from the tag: `en-GB` → `EN-GB`, other `en-*` → `EN-US`, `pt-BR` → `PT-BR`, other `pt-*` → `PT-PT`, else the
    code upper-case. Unsupported target (DeepL answers 400) → our 400 with a clear message.
  - Swiss German: DeepL has no `de-CH`; when the target tag is `de-CH` replace `ß` with `ss` in the results.
  - Errors: 403 → "DeepL key rejected", 429 → "DeepL is rate limiting, try again", 456 → "DeepL quota exhausted";
    all as `TranslationException(message)`; network/timeouts too (HttpClient timeout ~30 s).
- `TranslateController` `POST /api/translate` body `TranslateRequest(string Target, List<string> Texts)` → `TranslateResponse(List<string> Texts)`:
  `[Authorize(Policy = StaffOnly)]`, rate limit policy `pages`, `[RequestSizeLimit(512 * 1024)]` (+ nginx
  `location = /api/translate { client_max_body_size 512k; … }` like `/api/menus`); 404 when off; 400 when `Target` isn't
  a configured non-default code, > 500 texts or a text > 10 000 chars; 502 ProblemDetails with the
  `TranslationException` message. Source = the default language. Add the policy override note in `ApiFactory` if a new
  policy is introduced (prefer reusing `pages`).
- OpenAPI changes → `pnpm build:back` + `pnpm gen-api`.

### Editor
- `PageSettings.vue` "Translations" section (from Phase 3) and the header language menu, only when
  `useInstanceStore().config.machineTranslation` and `editLang` isn't the default:
  - **Translate missing fields**: collect (block id, attribute key, default value) for every translatable attribute
    with a non-empty default value and no translation in `editLang`, plus page title/meta; one `POST /api/translate`;
    apply all results with a new store action `applyTranslations(code, entries, pageText)` that pushes **one** history
    snapshot and one `trackChange`. Toast "N fields translated by DeepL, please review" (`useToast`).
  - **Re-translate all** (secondary, behind `useConfirm` with a danger confirm): same, but for every translatable field,
    overwriting existing translations.
  - Disabled while unsaved changes are being saved? No: it only edits the working copy like typing does. Disabled while
    a request is running; errors go to the editor error banner (`store.errorMessage`).
- Messages in `locales/admin/*.json` (4 languages).

## Tests
- xUnit unit `DeepLTranslatorTests` with a fake `HttpMessageHandler`: free vs pro host, auth header, batching of 120
  texts into 3 calls in order, empty strings untouched, `EN-US`/`EN-GB`/`PT-*` mapping, `ß` → `ss` for `de-CH` only,
  403/429/456 → messages.
- xUnit integration `TranslateTests`: 401 anonymous, 403 customer, 404 when off, 400 for default/unknown target and
  oversize, 200 with a fake `ITranslator` (`ConfigureTestServices`), `InstanceConfig.machineTranslation`.
- Vitest: collecting missing fields from a tree (pure helper, e.g. `lib/web-editor/core/translatable.ts`, shared with
  Phase 3's "N untranslated" count) and `applyTranslations` = one undo step.
- e2e `editor/deepl.spec.ts`: enable via `page.route('**/api/public/instance', …)` returning `machineTranslation: true`
  and `page.route('**/api/translate', …)` returning upper-cased texts; click "Translate missing fields" in `de`, the
  paragraph shows the stubbed text, Ctrl+Z reverts all fields at once. CI needs no key.

## Docs to update
`claude-context/ARCHITECTURE.md` (Localization: DeepL flow; **privacy**: page text is sent to DeepL — Pro does not
store texts, the free plan may use them; site owners must know before a key is set; cost = characters translated),
`frontend/docs/INSTANCE_GUIDE.md` (enable with `DEEPL_API_KEY`, privacy note), `api-backend/CLAUDE.md`, root `CLAUDE.md`
env table, `.env.example`, `frontend/src/lib/web-editor/CLAUDE.md`, this file's status line, #45 (close it when this
lands, with `state_reason: completed`).
