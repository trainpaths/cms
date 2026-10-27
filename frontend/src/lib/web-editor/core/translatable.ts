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
