import { computed } from 'vue'
import { useEditorStore } from '../../../stores/editor'
import { DEFAULT_LANGUAGE } from '../../../i18n/languages'

type Field = 'title' | 'metaTitle' | 'metaDescription'
const columns = { title: 'pageTitle', metaTitle: 'pageMetaTitle', metaDescription: 'pageMetaDescription' } as const

/**
 * Page title / meta in the editor's language: the default language edits the page's own field, another language
 * its translation (`store.pageTranslations`). `original` = the default's value while translating (placeholder), else ''.
 */
export function usePageText(field: Field) {
	const store = useEditorStore()
	const translating = computed(() => store.editLang !== DEFAULT_LANGUAGE.code)
	const value = computed({
		get: () => (translating.value ? (store.pageTranslations[store.editLang]?.[field] ?? '') : store[columns[field]]),
		set: (next: string) => {
			if (translating.value) store.setPageText(store.editLang, { [field]: next })
			else store[columns[field]] = next
		},
	})
	const original = computed(() => (translating.value ? store[columns[field]] : ''))
	return { value, original, translating }
}
