# CMS instance guide

How to build a client site on the CMS. An **instance** is a small repo (start from the GitHub template
`trainpaths/cms-starter`) that imports the CMS and adds only what is specific to the client: its blocks, page
templates, component overrides, theme and configuration. The CMS itself (admin, editor, public site renderer, API)
stays in `trainpaths/cms` and is upgraded by bumping one version.

This file ships inside the package: `node_modules/@trainpaths/cms/docs/INSTANCE_GUIDE.md`.

## What comes from where

| Part | Comes from | Instance controls |
|------|------------|-------------------|
| Admin SPA, block editor, public site app | `@trainpaths/cms` (source package, git tag) | blocks, templates, overrides, theme |
| Renderer (server-side rendering of public pages) | built in the instance (its SSR bundle contains the instance's blocks) | nothing extra |
| API (.NET) | prebuilt image `ghcr.io/trainpaths/cms-api:<version>` | `cms.config.json` (copied into the image) |
| Database, media storage | Postgres + SeaweedFS (S3) containers | `.env` |

**One version for both**: the package tag and the image tag must match (`v1.4.0` ↔ `1.4.0`); they share the API
contract.

## Instance repo layout

```
cms.config.json        instance config, read by the API (see below); "$schema" gives editor autocompletion
package.json           "@trainpaths/cms": "github:trainpaths/cms#vX.Y.Z&path:/frontend", "@trainpaths/nb-ui" (the tag
                       the CMS's peerDependencies name: pnpm doesn't install git packages as sub-dependencies), vue,
                       pinia, vue-router, vite, tailwindcss, typescript, vue-tsc
vite.config.ts         plugins: [cms()]   (import { cms } from '@trainpaths/cms/vite')
tsconfig.app.json      "extends": "@trainpaths/cms/tsconfig.json" (vue-tsc also type-checks the package source)
index.html             admin SPA shell → src/main.ts
public.html            public page template: keep <!--app-head-->, <!--app-html-->, <!--app-state--> and
                       <script type="application/json" id="__STATE__">; <html lang> is set at build time from the config
src/main.ts            import './style.css'; createAdmin()          from '@trainpaths/cms/admin'
src/entry-client.ts    import './style.css'; hydratePublic()        from '@trainpaths/cms/public/client'
src/entry-server.ts    export { render }                             from '@trainpaths/cms/public/server'
src/style.css          @import '@trainpaths/cms/style.css'; + the site theme
src/blocks/<name>/     instance blocks
src/templates/<name>.vue   template pages
src/overrides/<Name>.vue   component overrides (nb-ui components: src/overrides/ui/<Name>.vue)
Dockerfile, docker-compose.yml, .env.example
```
Build: `vue-tsc -b && vite build && vite build --ssr src/entry-server.ts --outDir dist-ssr`.

## `cms.config.json`

Validated strictly when the API starts: an unknown key or invalid value stops it with a message naming the problem.
No file = defaults. Full schema: `node_modules/@trainpaths/cms/cms.config.schema.json`.

```jsonc
{
	"$schema": "./node_modules/@trainpaths/cms/cms.config.schema.json",
	"publicAuth": false,                 // customer accounts (/login, /register...); off: screens + API gone
	"site": { "lang": "de" },            // <html lang> (set by cms() at build time: rebuild the frontend)
	"blocks": { "exclude": ["list"] },   // built-in blocks hidden from the inserters
	"siteConfig": {                      // what the owner fills in under Configuration (see Site config)
		"fields": [{ "key": "vatId", "label": "VAT ID" }],
		"groups": [{
			"key": "socials", "label": "Social media", "allowCustom": false,
			"presets": [
				{ "key": "instagram", "label": "Instagram", "type": "link" },
				{ "key": "linkedin", "label": "LinkedIn", "type": "link" }
			]
		}]
	},
	"pages": [                           // seeded pages; omitted = Home, Privacy Policy, Legal
		{ "slug": "home", "title": "Home", "blocks": [] },
		{ "slug": "privacy-policy", "title": "Privacy Policy", "footer": true, "blocks": [] },
		{ "slug": "legal", "title": "Legal notice", "template": "legal", "footer": true }
	]
}
```

**Pages**: seeded published, all of them on a fresh install, locked/template pages again on every start when missing.
- `template`: rendered by `src/templates/<template>.vue` (implies `locked`).
- `locked`: slug fixed, page can't be deleted (the site's code links to it).
- `editable: false` (template pages only): the owner edits title/settings but no blocks.
- `footer`: linked in the public footer while published, in config order.
- `blocks`: initial content (`{ id, name, attributes, innerBlocks }`), seeded once.
Page flags are looked up by slug: removing a page from the config just unlocks it.

## Blocks

Same folder format as the built-ins (`node_modules/@trainpaths/cms/src/lib/web-editor/blocks/`, e.g. `heading`):
```
src/blocks/callout/
  block.json        { "name": "callout", "title": "Callout", "category": "text", "description": "...",
                      "supports": { "width": true, "backgroundColor": true, "textColor": true } }
  index.ts          export default defineBlock(meta, { icon, attributes: { text: { type: 'string', default: '' } },
                      edit, settings? })
  CalloutEdit.vue   editor UI, props { block }
  CalloutView.vue   public render, props { block }
  CalloutIcon.vue   inline SVG, stroke="currentColor"
  CalloutSettings.vue   optional right-sidebar panel
```
- Edit side imports from **`@trainpaths/cms/editor`** (`defineBlock`, `useBlockAttribute`, `useSelection`,
  `InlineToolbar`, `ToolbarDropdown`, `SettingsSection`, `BlockList`, `MediaActions`, `MediaField`, `useAutoResize`,
  types). The View imports from **`@trainpaths/cms/site`** only: it is server-rendered and shipped to visitors, the
  editor barrel would drag the editor into the public bundle.
- Write attributes only through `useBlockAttribute` (undo history and autosave depend on it).
- Attribute values: string, number or boolean (the API rejects anything else). Repeating content = inner blocks
  (`allowedBlocks` + `<BlockList>` / `<ViewBlockList>`). Images: the `mediaId` attribute (one per block), rendered with
  `useMediaImage`.
- Edit and View look the same: same root padding class (`p-16`), container-query variants only (`@md:`, never `md:`).
- Views: no `window`/`document` in setup (server-rendered; check the browser console for hydration warnings).
- A folder whose `block.json` uses a built-in's `name` **replaces** that block (only `block.json` + `*View.vue` =
  replaces just its public view). New block folders need a dev server restart.
- Example: `playground/src/blocks/callout/` in the CMS repo.

## Template pages

`src/templates/<name>.vue`, for pages whose config entry says `"template": "<name>"`. Props: `page` (title, slug,
blocks, media, ...) and `config` (the site config, see below). Typical: wrap the default layout and add data:
```vue
<script setup lang="ts">
import { PageContent, SiteConfigEntry, filledEntries, firmName, type TemplateProps } from '@trainpaths/cms/site'
const props = defineProps<TemplateProps>()
</script>
<template>
	<PageContent :title="page.title" :blocks="page.blocks" :media="page.media">
		<template #after>
			<p>{{ firmName(config) }}</p>
			<p v-for="e in filledEntries(config, 'contact')" :key="e.id">{{ e.label }}: <SiteConfigEntry :entry="e" /></p>
		</template>
	</PageContent>
</template>
```
`PageContent` has `before` / `after` slots around the owner's blocks; a template may also render everything itself
(then set `"editable": false`). A missing template file falls back to the default layout (console warning).

## Overrides

`src/overrides/<Name>.vue` replaces the package's `<Name>.vue` wherever the CMS uses it (resolved by `cms()`; restart
the dev server after adding/removing one). Wrap the original instead of copying it:
```vue
<script setup lang="ts">
import Base from '@trainpaths/cms/src/components/SiteFooter.vue'
</script>
```
**Stable override points** (renamed or re-propped only in a major version): `SiteHeader.vue` (props `config`,
`menu`), `SiteFooter.vue` (`config`), `NotFound.vue`, `PageContent.vue` (`title`, `blocks`, `media`, slots
`before`/`after`), every block's `*View.vue` / `*Edit.vue` (`block`), and nb-ui components
(`src/overrides/ui/<Name>.vue`). Overriding anything else works, but may break on any upgrade.

## Theme

`src/style.css`:
```css
@import '@trainpaths/cms/style.css';

@theme {                       /* new tokens → utilities for your blocks/templates (bg-brand, text-brand...) */
	--color-brand: #0f766e;
}

.site-theme {                  /* the public site + the editor canvas; the admin keeps the CMS look */
	--color-primary: var(--color-brand);
	--font-sans: 'Inter Variable', sans-serif;
}
```
- Tokens: nb-ui's (`--color-primary`, `-primary-dark`, `-secondary`, `-accent`, ... `--font-sans`, `--nb-rounded`);
  spacing unit is 1px (`p-16` = 16px).
- Fonts: self-host them (e.g. `@fontsource-variable/inter`, imported in `style.css`): the CSP allows `font-src 'self'`.
- Everything public is same-origin: CSP `default-src 'self'`, images only from the media library. External
  embeds/scripts/fonts are not allowed (yet).

## Site config (Configuration)

The owner fills fixed **fields** (core: `firmName`) and **groups** of entries (core: `contact` with address, phone,
email; plus your `siteConfig.groups`). Presets fix an entry's key and type (`text`, `email`, `phone`, `link`,
`address`); `allowCustom` lets the owner add own entries; `required` presets can't be removed; `defaults` start a
fresh config. A group or field with a core key replaces the core one.

Read it in templates/overrides (the `config` prop, or `useSiteConfigStore().config`):
```ts
config.fields.firmName                       // string ('' when empty)
firmName(config)                             // the same, trimmed, null-safe
config.groups.socials                        // ConfigEntry[]: { id, key, label, type, value, address }
configEntry(config, 'contact', 'email')      // one entry by key
filledEntries(config, 'contact')             // entries with a value
addressLines(entry.address)                  // ['Main St 1', '12345 Town', 'Germany']
entryHref(entry) / entryText(entry)          // mailto:/tel:/URL, display text
emptyAddress()                               // { street: '', postalCode: '', city: '', country: '' }
```
`<SiteConfigEntry :entry="e" />` renders one entry's value like the footer does (address lines, mailto/tel/link,
text). All from `@trainpaths/cms/site`. The default footer shows logo, firm name, the contact group and the footer pages;
show other groups by overriding `SiteFooter.vue`.

## Page meta

Owners set a meta title and description per page (editor → Page → Search & sharing). Document title:
"{meta title} - {firm name}", just the firm name without a meta title. Description: meta description, else the first
paragraph. Link-preview image: the page's first image, else the share image from Configuration.

## Running and deploying

- Dev: the API stack in Docker (postgres, seaweedfs, `cms-api` with your `cms.config.json` mounted at
  `/app/cms.config.json`) + `vite` (dev server renders public pages itself; `/api` proxied to `VITE_API_BASE_URL`).
- Images (see the starter's Dockerfile): **frontend** (nginx with `node_modules/@trainpaths/cms/nginx.conf` + your
  `dist/`), **renderer** (Node: `server/render-server.js` + `template.js` from the package, `dist/public.html`,
  `dist-ssr/`; run from that directory), **api** (`FROM ghcr.io/trainpaths/cms-api:<version>` +
  `COPY cms.config.json /app/`).
- Environment: the CMS repo's `.env.example` lists every variable (DB, JWT key, S3 keys, SMTP, first admin).

## Upgrading

1. Read the release notes between your version and the target (GitHub releases of `trainpaths/cms`); a major
   version lists what breaks (config keys, override points, block contract, entry stubs).
2. Bump **both**: the package tag in `package.json` and the API image tag (and `@trainpaths/nb-ui` to the CMS's
   `peerDependencies` value), then `pnpm install`. Instances from `trainpaths/cms-starter` do all of this with
   `pnpm bump-cms [X.Y.Z]` (default: latest release; also checks the tag and image exist).
3. `pnpm build`; fix type errors (your overrides/blocks are checked against the new package source).
4. Back up the database: the API applies its migrations on start, there is no downgrade.
