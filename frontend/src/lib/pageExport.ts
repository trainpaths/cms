import type { ImportPageRequest } from '../api/types.gen'
import type { BlockInstance, PageTextTranslation } from './web-editor/core/types'

/**
 * Page export file: one page's content as JSON, to keep a copy or move it to another page/site.
 * Importing always creates a new draft (`POST /api/pages/import`); media is referenced by id only,
 * so images resolve on the same site and are reported missing elsewhere.
 */
export const PAGE_EXPORT_FORMAT = 'cms-page'
/**
 * 2: + `translations` (title/meta per language; block translations ride inside `blocks` in both). 1 still imports.
 * Translation source hashes (`translationSources`, `translations[code].sources`) need no new version: an API that
 * doesn't know them ignores them.
 */
export const PAGE_EXPORT_VERSION = 2

export interface PageExportContent {
	title: string
	slug: string
	metaTitle: string
	metaDescription: string
	translations: Record<string, PageTextTranslation>
	tags: string[]
	blocks: BlockInstance[]
}

export interface PageExport {
	format: typeof PAGE_EXPORT_FORMAT
	version: number
	exportedAt: string
	page: PageExportContent
}

export function toPageExport(page: PageExportContent, now = new Date()): PageExport {
	const { title, slug, metaTitle, metaDescription, translations, tags, blocks } = page
	return {
		format: PAGE_EXPORT_FORMAT,
		version: PAGE_EXPORT_VERSION,
		exportedAt: now.toISOString(),
		page: { title, slug, metaTitle, metaDescription, translations, tags: [...tags], blocks },
	}
}

/** A file that isn't a usable page export; the message (English) / `key` (message key) is meant for the user. */
export class PageExportError extends Error {
	readonly key: string
	constructor(message: string, key: string) {
		super(message)
		this.key = key
	}
}

export function pageExportFileName(slug: string): string {
	return `${slug || 'page'}.json`
}

/**
 * Reads an export file into the import request. Throws a `PageExportError` when the file
 * isn't a page export; the block tree itself is validated by the API.
 */
export function parsePageExport(text: string): ImportPageRequest {
	let data: unknown
	try {
		data = JSON.parse(text)
	} catch {
		throw new PageExportError('The file is not valid JSON.', 'admin.pageExport.invalidJson')
	}
	if (!isObject(data) || data.format !== PAGE_EXPORT_FORMAT) throw new PageExportError('The file is not a page export.', 'admin.pageExport.notExport')
	if (typeof data.version !== 'number' || data.version > PAGE_EXPORT_VERSION)
		throw new PageExportError('The file was exported by a newer CMS version.', 'admin.pageExport.newer')

	const page = data.page
	if (!isObject(page) || typeof page.title !== 'string' || !page.title.trim() || !Array.isArray(page.blocks))
		throw new PageExportError(
			'The page export is incomplete (title or blocks missing).',
			'admin.pageExport.incomplete',
		)
	const tags = Array.isArray(page.tags) ? page.tags.filter((t): t is string => typeof t === 'string') : []

	return {
		title: page.title,
		slug: optionalString(page.slug),
		metaTitle: optionalString(page.metaTitle),
		metaDescription: optionalString(page.metaDescription),
		tags,
		blocks: page.blocks,
		// v1: none; shape checked by the API
		...(isObject(page.translations) && {
			translations: page.translations as Record<string, PageTextTranslation>,
		}),
	}
}

function isObject(value: unknown): value is Record<string, unknown> {
	return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function optionalString(value: unknown): string | null {
	return typeof value === 'string' ? value : null
}
