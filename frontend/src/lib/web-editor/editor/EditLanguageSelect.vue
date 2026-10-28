<script setup lang="ts">
import { computed } from 'vue'
import { useEditorStore } from '../../../stores/editor'
import { IS_LOCALIZED, LANGUAGES } from '../../../i18n/languages'
import LanguagePicker from '../../../components/LanguagePicker.vue'

/**
 * Which language the editor edits (localized sites only): the default edits the page itself, another language the
 * translations of its text (title, meta, translatable block attributes). Badges count each language's untranslated
 * + outdated fields (the Page tab spells them out).
 * md+ only: no room in the phone header (the Page tab's Translations section switches there).
 */
const store = useEditorStore()

const counts = computed(() =>
	Object.fromEntries(
		LANGUAGES.map(({ code }) => {
			const status = store.translationStatus[code]
			return [code, status ? status.missing + status.outdated : 0]
		}),
	),
)
</script>

<template>
	<div
		v-if="IS_LOCALIZED"
		class="shrink-0 max-md:hidden"
		data-testid="edit-language"
	>
		<LanguagePicker
			:current="store.editLang"
			:label="$t('editor.translations.editLanguage')"
			:counts="counts"
			@select="store.editLang = $event"
		/>
	</div>
</template>
