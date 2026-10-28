<script setup lang="ts">
import { useLanguageSwitch } from '../i18n'
import LanguagePicker from './LanguagePicker.vue'

/**
 * Language switch for the public site (footer, header, templates) and the admin. Renders nothing on a
 * single-language site. Picking a language remembers it in the `cms_lang` cookie; the app decides the rest (admin:
 * strings switch in place, public site: the same page in that language). The current language comes from the app's
 * state, so the server render and the hydration agree.
 */
withDefaults(defineProps<{ align?: 'start' | 'end' }>(), { align: 'start' })

const languageSwitch = useLanguageSwitch()
</script>

<template>
	<div
		v-if="languageSwitch && languageSwitch.languages.length > 1"
		class="inline-flex"
		data-testid="language-dropdown"
	>
		<LanguagePicker
			:current="languageSwitch.current.value.code"
			:label="$t('common.language')"
			:align="align"
			@select="languageSwitch.change"
		/>
	</div>
</template>
