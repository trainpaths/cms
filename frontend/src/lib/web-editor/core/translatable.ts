import { getBlockType } from './blockRegistry'
import type { BlockInstance, PageTextTranslation } from './types'

/** Whether a block type's attribute is `translatable` (unknown block or attribute: no). */
export const isTranslatable = (blockName: string, key: string) =>
	!!getBlockType(blockName)?.attributes[key]?.translatable

/** One translatable text field: a block attribute, or a page field (`blockId` null: title / metaTitle / metaDescription). */
export interface TextField {
	blockId: string | null
	key: string
	/** The default language's value (the source). */
	source: string
}

export type PageText = Pick<PageTextTranslation, 'title' | 'metaTitle' | 'metaDescription'>

/**
 * Translatable fields with a non-empty default value, in tree order, page fields first. `missingIn`: only those
 * without a translation in that language (the "N untranslated" count, DeepL "translate missing").
 */
export function textFields(
	blocks: BlockInstance[],
	page: PageText,
	translations: Record<string, PageTextTranslation>,
	missingIn?: string,
): TextField[] {
	const fields: TextField[] = []
	const pageText = missingIn ? translations[missingIn] : undefined
	for (const key of ['title', 'metaTitle', 'metaDescription'] as const)
		if (page[key].trim() && !(missingIn && pageText?.[key])) fields.push({ blockId: null, key, source: page[key] })
	const walk = (list: BlockInstance[]) => {
		for (const block of list) {
			for (const [key, value] of Object.entries(block.attributes)) {
				if (typeof value !== 'string' || !value.trim() || !isTranslatable(block.name, key)) continue
				const translated = missingIn ? block.translations?.[missingIn]?.[key] : undefined
				if (translated === undefined || translated === '') fields.push({ blockId: block.id, key, source: value })
			}
			walk(block.innerBlocks)
		}
	}
	walk(blocks)
	return fields
}

/**
 * Untranslated fields per language code (`codes`), in **one** walk of the tree: what `textFields(…, code).length` gives
 * for each code (editor counts, on every change).
 */
export function missingCounts(
	blocks: BlockInstance[],
	page: PageText,
	translations: Record<string, PageTextTranslation>,
	codes: readonly string[],
): Record<string, number> {
	const counts = Object.fromEntries(codes.map((code) => [code, 0]))
	const count = (translated: (code: string) => unknown) => {
		for (const code of codes) if (translated(code) === undefined || translated(code) === '') counts[code]!++
	}
	for (const key of ['title', 'metaTitle', 'metaDescription'] as const)
		if (page[key].trim()) count((code) => translations[code]?.[key])
	const walk = (list: BlockInstance[]) => {
		for (const block of list) {
			for (const [key, value] of Object.entries(block.attributes))
				if (typeof value === 'string' && value.trim() && isTranslatable(block.name, key))
					count((code) => block.translations?.[code]?.[key])
			walk(block.innerBlocks)
		}
	}
	walk(blocks)
	return counts
}

/**
 * A copy of `blocks` without translations of attributes their (registered) block type doesn't mark `translatable`:
 * the API overlays any stored key on the public site, so a stale one (attribute no longer translatable, imported
 * file) would show there while the editor can't. Blocks of unknown types keep theirs. What a save sends.
 */
export function pruneTranslations(blocks: BlockInstance[]): BlockInstance[] {
	return blocks.map(({ translations, ...block }) => {
		const innerBlocks = pruneTranslations(block.innerBlocks)
		if (!translations) return { ...block, innerBlocks }
		const known = !!getBlockType(block.name)
		const kept: Record<string, Record<string, unknown>> = {}
		for (const [code, values] of Object.entries(translations)) {
			const keys = Object.entries(values).filter(([key]) => !known || isTranslatable(block.name, key))
			if (keys.length) kept[code] = Object.fromEntries(keys)
		}
		return Object.keys(kept).length ? { ...block, translations: kept, innerBlocks } : { ...block, innerBlocks }
	})
}
