import type { BlockInstance, PublicPage, SiteConfig } from '../lib/web-editor'
import { firmName } from '../lib/siteConfig'
import { pathOf, type PublicState } from './state'

const DESCRIPTION_MAX = 160
/**
 * Admin/CMS icon (package public/favicon.svg); used when the site config has no icon, and in the admin chrome. Bind it
 * (`:src`): a static `src` is resolved as a module at build time, and the file isn't in the app's own publicDir.
 */
export const CMS_ICON = '/favicon.svg'

/**
 * Document title (site meta rule): "{meta title} - {firm name}", just the firm name when the page has no meta title;
 * without a firm name the meta title, then the page title.
 */
export function documentTitle(page: Pick<PublicPage, 'title' | 'metaTitle'> | null, config: SiteConfig | null): string {
	const firm = firmName(config)
	if (!page) return firm ? `Page not found - ${firm}` : 'Page not found'
	const meta = page.metaTitle.trim()
	if (firm) return meta ? `${meta} - ${firm}` : firm
	return meta || page.title
}

/** Document title of a public page (also used by client navigation). */
export const pageTitle = (state: Pick<PublicState, 'page' | 'config'>) => documentTitle(state.page, state.config)

const escapeAttr = (value: string) =>
	value.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

function* walk(blocks: BlockInstance[]): Generator<BlockInstance> {
	for (const block of blocks) {
		yield block
		yield* walk(block.innerBlocks)
	}
}

/** First paragraph text, whitespace collapsed, cut at a word boundary. No SEO field yet. */
export function description(blocks: BlockInstance[]): string {
	for (const block of walk(blocks)) {
		if (block.name !== 'paragraph') continue
		const text = String(block.attributes.text ?? '')
			.replace(/\s+/g, ' ')
			.trim()
		if (!text) continue
		if (text.length <= DESCRIPTION_MAX) return text
		const cut = text.slice(0, DESCRIPTION_MAX - 1)
		const space = cut.lastIndexOf(' ')
		return `${space > 80 ? cut.slice(0, space) : cut}…`
	}
	return ''
}

/** URL of the first image (image or card block) on the page, else the site's share image. */
function previewImage(state: PublicState): string | null {
	const page = state.page
	if (!page) return null
	for (const block of walk(page.blocks)) {
		const id = block.attributes.mediaId
		if (!id) continue
		const media = page.media.find((m) => m.id === id)
		if (media) return media.url
	}
	return state.config?.shareImage?.url ?? null
}

/**
 * `<head>` tags for a server-rendered public page: title, description (meta description, else the first paragraph),
 * canonical, Open Graph / Twitter card (og:title = meta title or page title, og:site_name = firm name, og:image =
 * first image or the site's share image), favicon. Link-preview scrapers (Slack, WhatsApp, LinkedIn...) run no JS,
 * so this is all they see.
 */
export function headTags(state: PublicState): string {
	const title = pageTitle(state)
	const icon = state.config?.icon?.url ?? null
	const tags = [`<title>${escapeAttr(title)}</title>`]

	if (icon) tags.push(`<link rel="icon" href="${escapeAttr(icon)}">`)
	else tags.push(`<link rel="icon" type="image/svg+xml" href="${CMS_ICON}">`)

	if (!state.page) {
		tags.push('<meta name="robots" content="noindex">')
		return tags.join('\n')
	}

	const url = state.baseUrl + pathOf(state.page.slug)
	const desc = state.page.metaDescription.trim() || description(state.page.blocks)
	const image = previewImage(state)
	const firm = firmName(state.config)
	const meta = (attr: 'name' | 'property', key: string, value: string) =>
		`<meta ${attr}="${key}" content="${escapeAttr(value)}">`

	tags.push(`<link rel="canonical" href="${escapeAttr(url)}">`)
	if (desc) tags.push(meta('name', 'description', desc), meta('property', 'og:description', desc))
	tags.push(
		meta('property', 'og:type', 'website'),
		meta('property', 'og:title', state.page.metaTitle.trim() || state.page.title),
		meta('property', 'og:url', url),
	)
	if (firm) tags.push(meta('property', 'og:site_name', firm))
	if (image) tags.push(meta('property', 'og:image', state.baseUrl + image))
	tags.push(meta('name', 'twitter:card', image ? 'summary_large_image' : 'summary'))
	return tags.join('\n')
}

/** JSON for the `<script type="application/json">` state tag; `<` escaped so content can't close the tag. */
export const serializeState = (state: PublicState) => JSON.stringify(state).replace(/</g, '\\u003c')
