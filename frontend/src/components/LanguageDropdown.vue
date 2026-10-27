<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Button, Dropdown, Icon, type DropdownItem } from '@trainpaths/nb-ui'
import { languageLabel, useLanguageSwitch } from '../i18n'

/**
 * Language switch for the public site (footer, header, templates) and the admin. Renders nothing on a
 * single-language site. Picking a language remembers it in the `cms_lang` cookie; the app decides the rest (admin:
 * strings switch in place, public site: the same page in that language). The current language comes from the app's
 * state, so the server render and the hydration agree. nb-ui's Dropdown (Popover) touches `document` in setup, so
 * the server renders the trigger alone and the menu mounts on the client.
 */
withDefaults(defineProps<{ align?: 'start' | 'end' }>(), { align: 'start' })

const languageSwitch = useLanguageSwitch()
const mounted = ref(false)
onMounted(() => (mounted.value = true))

const items = computed<DropdownItem[]>(() =>
	(languageSwitch?.languages ?? []).map((language) => ({
		label: languageLabel(language.code),
		icon: language.code === languageSwitch?.current.value.code ? 'check' : undefined,
		onSelect: () => languageSwitch?.change(language.code),
	})),
)
</script>

<template>
	<div
		v-if="languageSwitch && languageSwitch.languages.length > 1"
		class="inline-flex"
		data-testid="language-dropdown"
	>
		<Dropdown
			v-if="mounted"
			:items="items"
			:align="align"
			:label="$t('common.language')"
		>
			<template #trigger="{ props }">
				<Button
					variant="ghost"
					size="sm"
					:lang="languageSwitch.current.value.tag"
					v-bind="props"
				>
					<Icon name="globe" />
					{{ languageLabel(languageSwitch.current.value.code) }}
				</Button>
			</template>
		</Dropdown>
		<!-- server render + hydration: the same trigger, no menu yet -->
		<Button
			v-else
			variant="ghost"
			size="sm"
			:lang="languageSwitch.current.value.tag"
			:aria-label="$t('common.language')"
		>
			<Icon name="globe" />
			{{ languageLabel(languageSwitch.current.value.code) }}
		</Button>
	</div>
</template>
