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
type PageField = keyof PageText
const PAGE_FIELDS: readonly PageField[] = ['title', 'metaTitle', 'metaDescription']

/**
 * A field in one language: `missing` (no or an empty translation), `outdated` (translated from a default text that
 * has changed since: the recorded hash differs), `ok`. A translation without a recorded hash counts as `ok`.
 */
export type TranslationState = 'missing' | 'outdated' | 'ok'

/**
 * Short hash of a default text (FNV-1a, 32 bit, 8 hex digits), recorded next to every translation written
 * (`translationSources`, page `sources`): the outdated check. Not security relevant.
 */
export function sourceHash(text: string): string {
	let hash = 0x811c9dc5
	for (let i = 0; i < text.length; i++) {
		hash ^= text.charCodeAt(i)
		hash = Math.imul(hash, 0x01000193)
	}
	return (hash >>> 0).toString(16).padStart(8, '0')
}

function stateOf(translated: unknown, recorded: string | undefined, currentHash: () => string): TranslationState {
	if (translated === undefined || translated === null || translated === '') return 'missing'
	return recorded && recorded !== currentHash() ? 'outdated' : 'ok'
}

/** State of a block's translatable attribute in language `code`. */
export function blockFieldState(block: BlockInstance, key: string, code: string): TranslationState {
	return stateOf(block.translations?.[code]?.[key], block.translationSources?.[code]?.[key], () =>
		sourceHash(String(block.attributes[key] ?? '')),
	)
}

/** State of a page field (title / meta) in language `code`. */
export function pageFieldState(
	page: PageText,
	translations: Record<string, PageTextTranslation>,
	key: PageField,
	code: string,
): TranslationState {
	const translation = translations[code]
	return stateOf(translation?.[key], translation?.sources?.[key] || undefined, () => sourceHash(page[key]))
}

/** Whether any translatable text of `block` (not its children) is outdated in `code` (editor marker). */
export function blockOutdated(block: BlockInstance, code: string): boolean {
	return Object.keys(block.translationSources?.[code] ?? {}).some(
		(key) => isTranslatable(block.name, key) && blockFieldState(block, key, code) === 'outdated',
	)
}

/** Translatable fields with a non-empty default value, in tree order, page fields first. */
function eachField(
	blocks: BlockInstance[],
	page: PageText,
	visit: (field: TextField, state: (code: string) => TranslationState) => void,
	translations: Record<string, PageTextTranslation>,
): void {
	for (const key of PAGE_FIELDS)
		if (page[key].trim())
			visit({ blockId: null, key, source: page[key] }, (code) => pageFieldState(page, translations, key, code))
	const walk = (list: BlockInstance[]) => {
		for (const block of list) {
			for (const [key, value] of Object.entries(block.attributes)) {
				if (typeof value !== 'string' || !value.trim() || !isTranslatable(block.name, key)) continue
				// one hash per field, whatever the number of languages
				let hash: string | undefined
				const current = () => (hash ??= sourceHash(value))
				visit({ blockId: block.id, key, source: value }, (code) =>
					stateOf(block.translations?.[code]?.[key], block.translationSources?.[code]?.[key], current),
				)
			}
			walk(block.innerBlocks)
		}
	}
	walk(blocks)
}

/**
 * Translatable fields with a non-empty default value, in tree order, page fields first. `missingIn`: only those
 * without a translation in that language (+ the outdated ones with `withOutdated`: DeepL "translate missing").
 */
export function textFields(
	blocks: BlockInstance[],
	page: PageText,
	translations: Record<string, PageTextTranslation>,
	missingIn?: string,
	withOutdated = false,
): TextField[] {
	const fields: TextField[] = []
	eachField(
		blocks,
		page,
		(field, state) => {
			const s = missingIn ? state(missingIn) : 'missing'
			if (s === 'missing' || (withOutdated && s === 'outdated')) fields.push(field)
		},
		translations,
	)
	return fields
}

export interface TranslationCounts {
	missing: number
	outdated: number
}

/** Missing and outdated fields per language code (`codes`), in **one** walk of the tree (editor counts). */
export function translationCounts(
	blocks: BlockInstance[],
	page: PageText,
	translations: Record<string, PageTextTranslation>,
	codes: readonly string[],
): Record<string, TranslationCounts> {
	const counts = Object.fromEntries(codes.map((code) => [code, { missing: 0, outdated: 0 }]))
	eachField(
		blocks,
		page,
		(_, state) => {
			for (const code of codes) {
				const s = state(code)
				if (s !== 'ok') counts[code]![s]++
			}
		},
		translations,
	)
	return counts
}

/**
 * A copy of `blocks` without translations (and their recorded sources) of attributes their (registered) block type
 * doesn't mark `translatable`: the API overlays any stored key on the public site, so a stale one (attribute no longer
 * translatable, imported file) would show there while the editor can't. Sources without a translation go too. Blocks
 * of unknown types keep theirs. What a save sends.
 */
export function pruneTranslations(blocks: BlockInstance[]): BlockInstance[] {
	return blocks.map(({ translations, translationSources, ...block }) => {
		const innerBlocks = pruneTranslations(block.innerBlocks)
		if (!getBlockType(block.name))
			return {
				...block,
				...(translations && { translations }),
				...(translationSources && { translationSources }),
				innerBlocks,
			}
		const kept: Record<string, Record<string, unknown>> = {}
		const sources: Record<string, Record<string, string>> = {}
		for (const [code, values] of Object.entries(translations ?? {})) {
			const keys = Object.entries(values).filter(([key]) => isTranslatable(block.name, key))
			if (!keys.length) continue
			kept[code] = Object.fromEntries(keys)
			const hashes = Object.entries(translationSources?.[code] ?? {}).filter(([key]) => key in kept[code]!)
			if (hashes.length) sources[code] = Object.fromEntries(hashes)
		}
		return {
			...block,
			...(Object.keys(kept).length > 0 && { translations: kept }),
			...(Object.keys(sources).length > 0 && { translationSources: sources }),
			innerBlocks,
		}
	})
}
