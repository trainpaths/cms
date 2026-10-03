# lib/web-editor — block editor library

Block editor, ported from the Nuxt project `gutenberg-copy` to plain Vue.
Concepts, contract and "add a block" checklist: `claude-context/BLOCK_SYSTEM.md` (repo root).

**Import from the barrel** (`index.ts`) in app code: `WebEditor`, `ViewBlockList`,
`registerAllBlocks`, `useEditorStore`, `useMediaStore`, `MediaGrid`, `MediaUploadButton`, `ApiError`,
`errorMessage`, limits, types. API calls go straight to the generated SDK (`src/api/sdk.gen`). Inside the library use
relative imports; there are no auto-imports (every `ref`/`computed`/component is imported explicitly).

## Files

```
index.ts            public API (barrel)
limits.ts           client-side mirrors of API validation (MEDIA_ACCEPT/MAX_BYTES/ALT_MAX/FILENAME_MAX, CONFIG_*,
                    TAG_* + normalizeTag, MENU_* + normalizeHandle)
core/types.ts       BlockInstance, BlockType (edit side; views are in core/blockViews.ts), BlockContext; page/media/config types
                    re-exported from the generated client (BlockInstance is structurally the API's Block)
core/blockRegistry.ts   Map registry + createBlockInstance (nanoid ids, attribute defaults)
core/blockTypes.ts  registerAllBlocks(): globs blocks/*/index.ts + the instance app's /src/blocks/*/index.ts (after the
                    built-ins: same name replaces); inserter order = registration order (built-ins alphabetical by folder, then app
                    blocks; a replacing block keeps the built-in's place) (idempotent; called by
                    editor/Editor.vue). No `view` field
core/blockViews.ts  getBlockView(name): globs blocks/*/*View.vue + /src/blocks/*/*View.vue (named via block.json); public
                    site + preview. Separate so the public site has no editor code
core/defineBlock.ts defineBlock(meta, definition): block.json + editor definition → BlockType
core/serializer.ts  serialize/parse helpers (unused by the store, kept for import/export)
blocks/<name>/      block.json (name, title, category, description, supports, parent?) + index.ts (`export default defineBlock(meta, {icon,
                    attributes, edit, settings?})`) + <Name>Edit/View/Icon.vue [+ <Name>Settings.vue]. Picked up
                    automatically: no central list (card, heading, paragraph, image, list, list-item, link)
editor/Editor.vue   editor for ONE page: props pageId/publicBase/previewPath, emits back; loads the page
                    (cached copy from usePagesStore first, then the server copy),
                    flushes unsaved changes on leave; hosts the error toast; non-editable config page
                    (store.pageEditable false) → template notice instead of the BlockList
editor/EditorHeader.vue   icon-only: back, left-sidebar toggle, title (saves on blur; hidden < sm → PageSettings shows it), undo/redo,
                    View/Preview link, Publish/Unpublish (= status, no badge; hidden < sm, PageSettings has the toggle), Save, right-sidebar toggle;
                    toggles show open state by colour (`.header-icon-btn--active`, `aria-pressed`)
editor/EditorLayout.vue   md+: flex row, left LeftSidebar (w-280) | scrolling <main> (flex-1, min-w-320, `@container/canvas`) | right
                          settings (w-280); canvas takes what the open sidebars leave. Below 320px of canvas the aside slots
                          shrink (min-w-0) and their absolute w-280 panels overlap the canvas (z-30). < md: sidebars are fixed
                          full-screen modals under the header, `#editor-mobile-toolbar` sticky bar at the top of
                          <main> (v-show while a block is selected). Canvas column is `@container` (blocks size by it) and
                          grows (flex) so the root BlockList fills it (drop below last block = append)
editor/BlockList.vue      recursive list + HTML5 drag/drop (move + create from sidebar, validity via
                          useBlockDrag; handlers stopPropagation so the innermost list decides); Inserter for nested lists.
                          Drop index from pointer vs block midpoints (upper half = before). Indicator slots (h-10) always
                          rendered; active line (bg-primary) grows 0 → 4px, 3px space each side: no layout shift. Root list with no blocks renders the
                          "Start adding blocks" placeholder, which is the drop target for the first block
editor/BlockWrapper.vue   selection/hover ring (no padding/border: blocks pad themselves), data-block-id, floating toolbar (grip = only drag source), #inline-toolbar-{id}
                          target, width/colours; no overflow-hidden (would clip child toolbars). Mobile: toolbar
                          `<Teleport :disabled>` into `#editor-mobile-toolbar` when selected, no hover toolbars.
                          wide/full overhang only when `main` has room (`@min-[784px]/canvas`, `@min-[816px]/canvas`;
                          ViewBlockWrapper uses the same widths as viewport variants)
editor/InlineToolbar.vue  <Teleport defer> into the wrapper toolbar (defer: target isn't attached yet on insert)
editor/Inserter.vue       inline "+ Block" buttons filtered by parent allowedBlocks
editor/SidebarTabs.vue    shared sidebar top row: tab bar (v-model, testids `sidebar-tab-{id}`) + close X (all sizes)
editor/LeftSidebar.vue    "Blocks" | "Outline" tabs (store.leftSidebarTab, survives hiding the sidebar)
editor/BlockOutline.vue   read-only tree of the page (BlockOutlineItem recursive, indent = nested ul pl-16); click selects +
                          scrolls the canvas; canvas selection expands collapsed ancestors (shared Set via outline.ts inject)
                          and scrolls the row into view
editor/scrollBlockIntoView.ts  nextTick + scroll canvas `[data-block-id]` to center (inserter + outline)
editor/BlockInserterSidebar.vue  search + categories; entries click-insert (after selection, climbing out of
                          parents whose allowedBlocks reject the type, then scroll into view) or drag in
editor/BlockSettingsPanel.vue    right sidebar: "Page" | "Block" tabs (store.rightSidebarTab; selecting a block → Block,
                          clearing → Page). Block = BlockType.settings + width/background/text colour per `supports`,
                          "No block selected" when empty. No deselect button: X only closes the sidebar
editor/PageSettings.vue   title (< sm only), slug (saved on blur/Enter via store.updateSlug; disabled when locked),
                          "Search & sharing": meta title (≤70) + description (≤200) with counters and the resulting tab
                          title (public/head documentTitle + site config firm name), saved on blur with the page
                          (store.pageMetaTitle/-Description); tags (`TagInput`, saved right away via store.updateTags,
                          not part of undo) + publish toggle
editor/ToolbarDropdown.vue  compact inline-toolbar choice (current value + menu); paragraph align, heading level
editor/ColorPicker.vue, SettingsSection.vue   settings UI pieces
view/ViewBlockList.vue → ViewBlockRenderer.vue (looks up `getBlockView(name)`, core/blockViews.ts) → ViewBlockWrapper.vue
composables/        useIsMobile (shared matchMedia ref, < 768px; store + BlockWrapper), useSelection (select/clear/prev/next/parent/child), useKeyboardNav (arrows, Esc, Delete;
                    undo Ctrl/Cmd+Z, redo Ctrl/Cmd+Shift+Z or Ctrl+Y), useToolbarPosition,
                    useBlockAttribute (writable computed over one attribute), useAutoResize (textarea grow),
                    useBlockDrag (shared dragged-block + dropTarget state, one indicator app-wide; canDropInto:
                    allowedBlocks/parent/no self-nesting)
media/              useMediaImage (block's `mediaId` → src/alt/missing from the media store;
                    `media/assignMedia.ts` sets/clears the id: editor only, kept apart so views don't import the editor store), MediaActions ("Upload new" + "Choose existing"; with `replace` both sit
                    behind one "Replace" menu, kept mounted via v-show so uploads can finish; errors → `onError` prop, default the editor banner;
                    also used outside the editor by the site config logo/icon pickers),
                    MediaField (settings panel: preview, actions, alt of the media object, remove),
                    MediaPickerDialog (teleported modal, grid + upload; stops keydown so editor shortcuts
                    don't fire), MediaGrid (`gridClass` overrides the auto-fill columns), MediaUploadButton (hidden file input, data-testid media-upload-input)
web-editor.css       @apply component classes (.input-field, .field-label, .inner-blocks-container,
                    .inner-blocks-label, .toolbar-input, ...) + scrollbar-thin/-stable utilities;
                    imported by src/style.css
```

## Editor store (lives outside the lib: `src/stores/editor.ts`)

`useEditorStore`: blocks tree ops, selection, UI toggles, snapshot undo/redo (50), page state
(pageId/Title/Slug/Status), loadPage/savePage/updateSlug/setPublished/closePage. Imports the lib's
`core/types` + `api.ts`; re-exported from the barrel.

## Gotchas

- Mutate blocks only through store actions (`updateBlockAttributes`, `addBlock`, ...): they push history
  and count towards autosave (every 4 changes). Saves are serialized in `savePage`.
- The editor store is a singleton: `Editor.vue` calls `loadPage` on mount / id change and `closePage` on back.
- SFC `<style>` blocks using `@apply` need `@reference "<relative path>/style.css";` (Tailwind v4).
- Block colours are user-chosen hex values, so wrappers use inline `:style` — the one allowed inline style.
- The canvas `<main>` is `.site-theme` (instance site theme). Editor chrome inside it (selection ring, drop lines,
  toolbar) uses `cms-primary` / `font-cms`, never `primary` / `font-sans`, or a site theme recolours it.
- Editor store page flags from the instance config: `pageTemplate`, `pageLocked` (slug input disabled),
  `pageEditable` (false: `savePage` sends the title only).
- Tailwind v3→v4 renames were applied during the port (`outline-none`→`outline-hidden`, `shadow-sm`→`shadow-xs`);
  write v4 class names.
- Wrappers add no spacing; each block pads its own root, same class in Edit and View (edit/view parity).
- Image `width` is a percent number (1–100); `blocks/image/width.ts` also reads legacy `"70%"` strings.
- Images reference media by id in the `mediaId` attribute (image + card; `media/useMediaImage`). Alt text
  lives on the media object (edited in the settings panel or `/admin/media`), fallback `"image"`; the caption
  is not an alt fallback. No external image URLs: nginx CSP is `img-src 'self'`.
- `data-testid`s used by e2e: `page-title`, `page-slug`, `publish-toggle`, `page-row`, `block-drag-handle`, `inserter-block`, `drop-indicator`,
  `media-upload`, `media-upload-input`, `media-choose`, `media-replace`, `media-picker`, `media-item`, `media-alt`, `media-name`, `media-details`, `media-save`, `media-delete`,
  `sidebar-tab-blocks`, `sidebar-tab-outline`, `outline-item`, `toggle-left-sidebar`, `toggle-right-sidebar`,
  `sidebar-close-left`, `sidebar-close-right`, `sidebar-tab-page`, `sidebar-tab-block`, `mobile-block-toolbar`, `page-title-mobile`.
- Blocks use container-query variants (`@md:`), never viewport `md:` (see BLOCK_SYSTEM.md). A `@container`
  element loses its content-based width: give it an explicit width (`w-full`) inside flex/shrink-to-fit parents.
- Mobile sidebar rules live in the editor store: closed by default on mobile, opening one closes the other,
  `closeInserterOnMobile()` after click-insert / outline select.
