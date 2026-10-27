<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Select } from '@trainpaths/nb-ui'
import { useEditorStore } from '../../../stores/editor'
import { DEFAULT_LANGUAGE, IS_LOCALIZED, LANGUAGES, languageLabel } from '../../../i18n/languages'
import { useMissingTranslations } from '../composables/useMissingTranslations'

/**
 * Which language the editor edits (localized sites only): the default edits the page itself, another language the
 * translations of its text (title, meta, translatable block attributes). Shows each language's untranslated count.
 */
const store = useEditorStore()
const { t } = useI18n()
const missing = useMissingTranslations()

const options = computed(() =>
	LANGUAGES.map(({ code }) => {
		const count = code === DEFAULT_LANGUAGE.code ? 0 : missing.value[code]!.length
		const name = languageLabel(code)
		return { value: code, label: count ? `${name} · ${t('editor.translations.missing', count)}` : name }
	}),
)
</script>

<template>
	<Select
		v-if="IS_LOCALIZED"
		v-model="store.editLang"
		:options="options"
		size="sm"
		class="w-auto max-w-200"
		:aria-label="$t('editor.translations.editLanguage')"
		:title="$t('editor.translations.editLanguage')"
		data-testid="edit-language"
	/>
</template>
