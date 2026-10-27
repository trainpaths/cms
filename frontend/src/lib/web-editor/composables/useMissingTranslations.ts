import { computed } from 'vue'
import { useEditorStore } from '../../../stores/editor'
import { DEFAULT_LANGUAGE, LANGUAGES } from '../../../i18n/languages'
import { textFields, type TextField } from '../core/translatable'

/** Untranslated text fields of the open page, per non-default language code. */
export function useMissingTranslations() {
	const store = useEditorStore()
	return computed(() => {
		const page = { title: store.pageTitle, metaTitle: store.pageMetaTitle, metaDescription: store.pageMetaDescription }
		const result: Record<string, TextField[]> = {}
		for (const { code } of LANGUAGES)
			if (code !== DEFAULT_LANGUAGE.code) result[code] = textFields(store.blocks, page, store.pageTranslations, code)
		return result
	})
}
