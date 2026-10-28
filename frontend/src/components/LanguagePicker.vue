<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Badge, Button, Dropdown, Flag, Icon, hasFlag, type DropdownItem } from '@trainpaths/nb-ui'
import { LANGUAGES, languageLabel } from '../i18n/languages'

/**
 * Language menu shared by the site/admin switch (LanguageDropdown) and the editor's edit language: trigger = flag +
 * short code (EN, DE), rows = flag + the language's name in itself. `counts` adds a badge per language (editor: fields
 * to translate). Opens above when there's no room below (footer, admin sidebar bottom). nb-ui's Dropdown (Popover)
 * touches `document` in setup, so the server renders the trigger alone and the menu mounts on the client.
 */
const props = withDefaults(
	defineProps<{
		current: string
		label: string
		align?: 'start' | 'end'
		counts?: Record<string, number>
	}>(),
	{ align: 'start', counts: undefined },
)

const emit = defineEmits<{ select: [code: string] }>()

const mounted = ref(false)
onMounted(() => (mounted.value = true))

const items = computed<DropdownItem[]>(() =>
	LANGUAGES.map(({ code }) => ({ label: code, onSelect: () => emit('select', code) })),
)
const tagOf = (code: string) => LANGUAGES.find((l) => l.code === code)?.tag
const triggerLabel = computed(() => `${props.label}: ${languageLabel(props.current)}`)
</script>

<template>
	<Dropdown
		v-if="mounted"
		:items="items"
		:align="align"
		placement="auto"
		:label="label"
	>
		<template #trigger="{ props: trigger }">
			<Button
				variant="ghost"
				size="sm"
				:aria-label="triggerLabel"
				:title="triggerLabel"
				v-bind="trigger"
			>
				<Flag
					v-if="hasFlag(current)"
					:code="current"
				/>
				<Icon
					v-else
					name="globe"
				/>
				{{ current.toUpperCase() }}
				<Badge
					v-if="counts?.[current]"
					size="sm"
					color="warning"
				>
					{{ counts[current] }}
				</Badge>
			</Button>
		</template>
		<template #item="{ item }">
			<Flag
				v-if="hasFlag(item.label)"
				:code="item.label"
			/>
			<Icon
				v-else
				name="globe"
			/>
			<span :lang="tagOf(item.label)">{{ languageLabel(item.label) }}</span>
			<Badge
				v-if="counts?.[item.label]"
				size="sm"
				color="warning"
			>
				{{ counts[item.label] }}
			</Badge>
			<Icon
				v-if="item.label === current"
				name="check"
				class="ml-auto"
			/>
		</template>
	</Dropdown>
	<!-- server render + hydration: the same trigger, no menu yet -->
	<Button
		v-else
		variant="ghost"
		size="sm"
		:aria-label="triggerLabel"
	>
		<Flag
			v-if="hasFlag(current)"
			:code="current"
		/>
		<Icon
			v-else
			name="globe"
		/>
		{{ current.toUpperCase() }}
	</Button>
</template>
