<script setup lang="ts">
import { computed } from 'vue'
import { Select } from '@trainpaths/nb-ui'
import { useEditorStore } from '../../../stores/editor'
import { IS_LOCALIZED, LANGUAGES, languageLabel } from '../../../i18n/languages'

/**
 * Which language the editor edits (localized sites only): the default edits the page itself, another language the
 * translations of its text (title, meta, translatable block attributes). Shows each language's count of untranslated
 * + outdated fields.
 * md+ only: no room in the phone header (the Page tab's Translations section switches there).
 */
const store = useEditorStore()

const options = computed(() =>
	LANGUAGES.map(({ code }) => {
		const status = store.translationStatus[code]
		const count = status ? status.missing + status.outdated : 0
		const name = languageLabel(code)
		// "Deutsch · 3": fields to do (the Page tab spells it out)
		return { value: code, label: count ? `${name} · ${count}` : name }
	}),
)
</script>

<template>
	<!-- wrapper: the Select root sets its own display -->
	<div
		v-if="IS_LOCALIZED"
		class="max-md:hidden"
	>
		<Select
			v-model="store.editLang"
			:options="options"
			size="sm"
			class="w-auto max-w-160"
			:aria-label="$t('editor.translations.editLanguage')"
			:title="$t('editor.translations.editLanguage')"
			data-testid="edit-language"
		/>
	</div>
</template>
