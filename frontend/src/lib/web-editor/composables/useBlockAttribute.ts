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
 * (empty = untranslated: `fallback`; text inputs use `useBlockText`, which also gives the default's text).
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
 * A translatable text attribute for a block's Edit component: `value` = `useBlockAttribute(block, key, '')` (the edited
 * language's text), `original` = the default language's text while another language is edited, else '' (greyed
 * placeholder: `:placeholder="original || $t('…')"`).
 */
export function useBlockText(
	block: () => BlockInstance,
	key: string,
): { value: WritableComputedRef<string>; original: ComputedRef<string> } {
	const store = useEditorStore()
	return {
		value: useBlockAttribute(block, key, ''),
		original: computed(() => (translating(store, block(), key) ? String(block().attributes[key] ?? '') : '')),
	}
}
