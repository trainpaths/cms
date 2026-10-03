import type { Component } from 'vue'
import type { MediaRef, PublicPage, SiteConfig } from '../lib/web-editor'

/** What a template page renders from: the public page (or the draft in the admin preview). */
export type TemplatePage = Pick<PublicPage, 'title' | 'slug' | 'blocks' | 'template'> & { media: MediaRef[] }

/** Props of an instance template (`src/templates/<name>.vue`). */
export interface TemplateProps {
	page: TemplatePage
	config: SiteConfig | null
}

// the instance app's templates (root-relative = its Vite root), by file name; a page's config names one
const modules = import.meta.glob<{ default: Component }>('/src/templates/*.vue', { eager: true })
const templates: Record<string, Component> = Object.fromEntries(
	Object.entries(modules).map(([path, mod]) => [path.slice(path.lastIndexOf('/') + 1, -'.vue'.length), mod.default]),
)
const warned = new Set<string>()

/** Template component for `name`; undefined (default layout) when the page has none or the file is missing. */
export function getTemplate(name: string | null | undefined): Component | undefined {
	if (!name) return undefined
	const template = templates[name]
	if (!template && !warned.has(name)) {
		warned.add(name)
		console.warn(`[cms] template "${name}" not found (src/templates/${name}.vue), using the default page layout`)
	}
	return template
}
