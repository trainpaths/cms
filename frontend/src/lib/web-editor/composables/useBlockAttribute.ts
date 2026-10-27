import { computed, type ComputedRef, type WritableComputedRef } from 'vue'
import { useEditorStore } from '../../../stores/editor'
import { DEFAULT_LANGUAGE } from '../../../i18n/languages'
import { isTranslatable } from '../core/translatable'
import type { BlockInstance } from '../core/types'

type AttrValue = string | number | boolean
// fallback '' / false would otherwise infer literal types
type Widen<T> = T extends string ? string : T extends number ? number : boolean

/** A translatable attribute while the editor edits another language than the default. */
function translating(store: ReturnType<typeof useEditorStore>, block: BlockInstance, key: string) {
	return store.editLang !== DEFAULT_LANGUAGE.code && isTranslatable(block.name, key)
}

/**
 * Writable computed over one block attribute. Writes go through `updateBlockAttributes`
 * (history + autosave), so toolbar and sidebar controls on the same attribute stay in sync.
 * A `translatable` attribute while the editor edits another language reads/writes that language's translation
 * (empty = untranslated: `fallback`; show the default's text with `useAttributeFallback`).
 */
export function useBlockAttribute<T extends AttrValue>(
	block: () => BlockInstance,
	key: string,
	fallback: T,
): WritableComputedRef<Widen<T>> {
	const store = useEditorStore()
	return computed({
		get: () => {
			const b = block()
			const value = translating(store, b, key) ? b.translations?.[store.editLang]?.[key] : b.attributes[key]
			return ((value as Widen<T>) || fallback) as Widen<T>
		},
		set: (value: Widen<T>) => {
			const b = block()
			if (translating(store, b, key)) store.updateBlockTranslation(b.id, store.editLang, { [key]: value })
			else store.updateBlockAttributes(b.id, { [key]: value })
		},
	})
}

/**
 * The default language's value of a translatable attribute while another language is edited (for a greyed
 * placeholder: `:placeholder="original || $t('…')"`), else ''.
 */
export function useAttributeFallback(block: () => BlockInstance, key: string): ComputedRef<string> {
	const store = useEditorStore()
	return computed(() => (translating(store, block(), key) ? String(block().attributes[key] ?? '') : ''))
}
