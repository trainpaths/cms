import type { Component } from 'vue'

// Namespace objects, not `import: 'default'`: container views import ViewBlockList, which imports this module
// (cycle); `.default` is only read at render time, when every module has finished loading.
// built-in blocks, then the instance app's `/src/blocks` (root-relative): same name replaces the built-in view
const views = {
	...import.meta.glob<{ default: Component }>('../blocks/*/*View.vue', { eager: true }),
	...import.meta.glob<{ default: Component }>('/src/blocks/*/*View.vue', { eager: true }),
}
const metas = {
	...import.meta.glob<{ name: string }>('../blocks/*/block.json', { eager: true, import: 'default' }),
	...import.meta.glob<{ name: string }>('/src/blocks/*/block.json', { eager: true, import: 'default' }),
}

/**
 * Public render component per block name: `blocks/<folder>/<Name>View.vue` (built-in or the app's
 * `src/blocks/`), named by the folder's block.json.
 * Separate from the registry (`blockTypes.ts`), so the public site and the SSR bundle carry no editor code.
 */
let byName: Record<string, { default: Component }> | null = null

export function getBlockView(name: string): Component | undefined {
	byName ??= Object.fromEntries(
		Object.entries(views).map(([path, mod]) => {
			const folder = path.slice(0, path.lastIndexOf('/'))
			return [metas[`${folder}/block.json`]!.name, mod]
		}),
	)
	return byName[name]?.default
}
