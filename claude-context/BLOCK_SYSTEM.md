# Block System

Code: `frontend/src/lib/web-editor/` (see its `CLAUDE.md` for the file map).

## Four layers
1. **Registry** (`core/blockRegistry.ts`) — a `Map<name, BlockType>`: what kinds of blocks exist.
   `registerAllBlocks()` (`core/blockTypes.ts`, idempotent, called by `editor/Editor.vue`) registers every
   built-in `blocks/*/index.ts`, then the instance app's `src/blocks/*/index.ts` (root-relative `import.meta.glob`;
   same `name` replaces the built-in and keeps its place). Inserter order = registration order: built-ins
   alphabetical by folder, then app blocks.
   Public render components are **not** in the registry: `core/blockViews.ts` globs `blocks/*/*View.vue` (+ the app's), so the
   server-rendered public site (`frontend/src/public/`) carries no editor code.
2. **Instances** — `createBlockInstance(name, overrides?)` fills attribute defaults and assigns a `nanoid()` id.
3. **Document tree** — the page is just `blocks: BlockInstance[]` in the Pinia store; nesting via `innerBlocks`.
4. **Serialization** — plain JSON (not WordPress HTML comments), stored as-is in the `pages.Blocks` jsonb column.

```ts
interface BlockInstance {
	id: string
	name: string                                            // registered BlockType.name
	attributes: Record<string, unknown>                     // string/number/boolean only (API validates)
	innerBlocks: BlockInstance[]                            // always an array
}
```

**Attributes vs inner blocks:** attributes are simple values that belong to the block (a card's `title`);
inner blocks are structured, repeatable children with their own editing (a card's links).

## BlockType
```ts
{
	name, title, category: 'text' | 'media' | 'containers' | 'embeds', icon, description?,
	supports?: { width?, backgroundColor?, textColor? },   // enables settings-panel sections
	attributes: Record<string, { type, default }>,
	allowedBlocks?: string[],                              // which children the block accepts
	parent?: string[],                                     // child-only: allowed parents, never top level
	edit: Component,                                       // editor UI, props { block }
	settings?: Component,                                  // block-specific sidebar panel, props { block }
}
```
- Metadata (name/title/category/description/supports/parent) lives in each block's `block.json`.
- `allowedBlocks` is enforced by the inline inserter (`editor/Inserter.vue`), the sidebar inserter
  (climbs out of parents that don't accept the block) and drag & drop (`useBlockDrag().canDropInto`,
  which also checks `parent`).
- Wrappers add **no spacing**: `editor/BlockWrapper.vue` is only selection (ring, no layout), toolbar and
  drag & drop; `view/ViewBlockWrapper.vue` only width + colours. **Each block pads its own root**, with the
  same class in `*Edit.vue` and `*View.vue` (text/image/link/list: `p-16`, list-item: `px-8 py-4`, card: its
  own box with `p-16` inside). Edit and view must look the same; lists use a 10px gap (editor: the
  drop-indicator slots; view: `gap-10`). Editor-only chrome (toolbar, "Content" label, inline inserter) is the only difference; container
  areas like `.inner-blocks-container` stay transparent so the block's background shows through.
- Shared attributes handled by wrappers, not by blocks: `blockWidth` (default/wide/full),
  `backgroundColor`, `textColor` → `editor/BlockWrapper.vue` and `view/ViewBlockWrapper.vue`.
- Colours are set inline on the wrapper and **inherited**: a block's own default text/bg class
  (`text-gray-700`, `bg-white`, ...) must only apply when the attribute is empty, or it overrides the choice.

## Adding a block type (checklist)
1. `blocks/<name>/block.json` — name, title, category, description, supports (+ `parent` for child-only blocks).
2. `blocks/<name>/<Name>Edit.vue` — props `{ block: BlockInstance }`; bind attributes with
   `useBlockAttribute(() => props.block, 'key', fallback)` (writes go through `store.updateBlockAttributes`;
   never mutate directly — history/autosave depend on it). Multi-line text: `<textarea>` + `useAutoResize`. Inline toolbar controls go in `<InlineToolbar :show="selected" :block-id="block.id">`.
   Containers render `<BlockList :blocks="block.innerBlocks" :parent-id="block.id" />`.
3. `blocks/<name>/<Name>View.vue` — read-only render; containers use `<ViewBlockList :blocks="block.innerBlocks" />`.
   Pad the root yourself (wrappers don't), with the same padding class as the Edit root.
4. `blocks/<name>/<Name>Icon.vue` — inline SVG, `stroke="currentColor"`.
4b. Optional `blocks/<name>/<Name>Settings.vue` — right-sidebar panel (`SettingsSection` + `.field-label`/
   `.input-field`), same `useBlockAttribute` bindings as the toolbar so both stay in sync.
5. `blocks/<name>/index.ts`: `export default defineBlock(meta, { icon, attributes, allowedBlocks?, edit, settings? })`
   (`meta` = `./block.json`). Nothing to register centrally: editor and
   public site find the folder by glob. Views are server-rendered: no `window`/`document` in setup.
6. Child-only blocks (like `list-item`): `"parent": ["list"]` in `block.json` hides them from the sidebar and
   blocks drops outside those parents.
7. Attribute values must stay string/number/boolean — the API rejects anything else.
7b. Images: store the media id in the `mediaId` attribute (the only attribute the API resolves into the
   page's `media[]`; one image per block). Use `media/useMediaImage` (src + alt from the media store, alt → `"image"`),
   `media/MediaActions` (Upload / Choose existing) in the block and `media/MediaField` in the settings
   panel; see `blocks/image` and `blocks/card`. Never store alt text on the block.
7c. Responsive layout: use **container-query variants only** (`@sm:`, `@md:`, ...), never viewport `md:`. The
   editor canvas column and the public `<article>` (`views/PageContent.vue`) are both `@container`, so a block
   follows the canvas width in the editor (sidebars open → narrower) and the page width when published.
   Same classes in Edit and View; see `blocks/card` (image beside text at `@md:`).
8. Add e2e coverage in `playground/e2e/tests/editor/blocks.spec.ts`.

**Instance blocks** (a client site): the same folder format in the instance app's `src/blocks/<folder>/`; imports from
`@trainpaths/cms/editor` (index.ts, Edit, Settings) and `@trainpaths/cms/site` (View: no editor code in the public
bundle) instead of relative paths. Example: `playground/src/blocks/callout/`. `blocks.exclude` in the instance config
hides built-ins from the inserters. A folder whose `block.json` reuses a built-in `name`
replaces that block; with only `block.json` + `*View.vue` it replaces just the public view.
No backend change is needed.

## Editor interaction model
- Click selects (`useSelection`); arrows move selection depth-first, ←/→ parent/first child, Esc clears,
  Delete/Backspace removes, Ctrl/Cmd+Z / Shift+Z undo/redo (`useKeyboardNav`; ignored inside inputs).
- Floating toolbar per block: move up/down, duplicate, remove, plus a `#inline-toolbar-{id}` teleport target.
  Below 768px (`useIsMobile`) the selected block's toolbar is teleported into a sticky bar at the top of the
  canvas (`#editor-mobile-toolbar`) and the sidebars are full-screen modals (one at a time).
- Drag & drop reorders within/between lists (`BlockList.vue`, HTML5 DnD). Only the toolbar's grip handle
  is `draggable` — a draggable wrapper would hijack text selection in inputs. Sidebar inserter entries are
  draggable too (create on drop). The dragged block lives in `useBlockDrag` (`getData` is unreadable during
  `dragover`); the innermost list decides and invalid targets show no indicator.
- Click-inserting from the sidebar scrolls the new block (`[data-block-id]`) into view.
- Choice controls in the inline toolbar use `editor/ToolbarDropdown.vue` (shows only the current value).
