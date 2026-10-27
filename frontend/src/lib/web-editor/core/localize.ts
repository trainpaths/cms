import type { BlockInstance, PageTextTranslation } from './types'

/**
 * A block tree in language `code`: attributes overlaid with that language's translations (non-empty strings, any
 * other primitive), `translations` removed. The default code just strips them. Mirrors `PageService.Localize` (API),
 * which does the same for the public site; the admin preview uses this one on the working copy.
 */
export function localizeBlocks(blocks: BlockInstance[], code: string, defaultCode: string): BlockInstance[] {
	return blocks.map((block) => {
		const attributes = { ...block.attributes }
		if (code !== defaultCode)
			for (const [key, value] of Object.entries(block.translations?.[code] ?? {}))
				if (value !== '' && value !== null && typeof value !== 'object') attributes[key] = value
		return {
			id: block.id,
			name: block.name,
			attributes,
			innerBlocks: localizeBlocks(block.innerBlocks, code, defaultCode),
		}
	})
}

/** Title/meta in `code`: the translation where non-empty, else the default's. */
export function localizeText<T extends { title: string; metaTitle: string; metaDescription: string }>(
	page: T,
	translations: Record<string, PageTextTranslation>,
	code: string,
	defaultCode: string,
): T {
	const text = code === defaultCode ? undefined : translations[code]
	return {
		...page,
		title: text?.title || page.title,
		metaTitle: text?.metaTitle || page.metaTitle,
		metaDescription: text?.metaDescription || page.metaDescription,
	}
}
