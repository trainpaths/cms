<script setup lang="ts">
import { computed } from 'vue'
import { Button, Input, Select } from '@trainpaths/nb-ui'
import { useI18n } from 'vue-i18n'
import { useConfigLabels } from '../../i18n/siteConfigLabels'
import {
	CONFIG_ADDRESS_PART_MAX,
	CONFIG_FIELD_TYPES,
	CONFIG_KEY_MAX,
	CONFIG_VALUE_MAX,
	type ConfigEntryValue,
	type ConfigFieldType,
	type ConfigPresetDef,
} from '../../lib/web-editor'
import { emptyAddress } from '../../lib/siteConfig'
import { INPUT_TYPES, TYPE_LABELS, valuePlaceholder } from './fieldInput'

/**
 * One entry of a site config group: a preset (name + type fixed by the instance config) or a custom entry (owner
 * names it and picks the type). Address entries get four inputs.
 */
const props = defineProps<{
	/** key of the entry's group (translated preset names) */
	group: string
	preset?: ConfigPresetDef
	required: boolean
	error: string
	first: boolean
	last: boolean
}>()
const entry = defineModel<ConfigEntryValue>({ required: true })
const emit = defineEmits<{ move: [delta: number]; remove: [] }>()

const { t } = useI18n()
const labels = useConfigLabels()
const typeOptions = computed(() => CONFIG_FIELD_TYPES.map((type) => ({ value: type, label: t(TYPE_LABELS[type]) })))

function setType(type: ConfigFieldType) {
	entry.value.type = type
	entry.value.address = type === 'address' ? (entry.value.address ?? emptyAddress()) : null
	if (type === 'address') entry.value.value = ''
}

const addressParts = [
	{ key: 'street', label: 'admin.config.address.street', testId: 'config-address-street' },
	{ key: 'postalCode', label: 'admin.config.address.postalCode', testId: 'config-address-postal' },
	{ key: 'city', label: 'admin.config.address.city', testId: 'config-address-city' },
	{ key: 'country', label: 'admin.config.address.country', testId: 'config-address-country' },
] as const
</script>

<template>
	<li
		class="flex flex-col gap-6 border-b border-gray-100 pb-12 last:border-b-0 last:pb-0"
		data-testid="config-entry"
	>
		<div class="grid grid-cols-[1fr_auto] gap-8 md:grid-cols-[180px_100px_1fr_auto] md:items-start">
			<template v-if="props.preset">
				<!-- top-aligned with the first input line (address entries are taller) -->
				<span
					class="self-start pt-8 text-sm font-medium text-gray-800"
					data-testid="config-entry-label"
					>{{ labels.entry(group, props.preset.key, props.preset.label) }}</span
				>
				<span class="hidden self-start pt-8 text-xs text-gray-400 md:block">{{
					$t(TYPE_LABELS[props.preset.type])
				}}</span>
			</template>
			<template v-else>
				<Input
					v-model="entry.key"
					:invalid="!!error"
					:maxlength="CONFIG_KEY_MAX"
					:placeholder="$t('admin.config.entryNamePlaceholder')"
					:aria-label="$t('admin.config.entryName')"
					data-testid="config-entry-key"
				/>
				<Select
					:model-value="entry.type"
					:options="typeOptions"
					:aria-label="$t('admin.config.entryType')"
					data-testid="config-entry-type"
					@update:model-value="setType($event as ConfigFieldType)"
				/>
			</template>

			<div
				v-if="entry.type === 'address' && entry.address"
				class="col-span-2 grid grid-cols-1 gap-8 sm:grid-cols-[120px_1fr] md:col-span-1"
			>
				<Input
					v-for="part in addressParts"
					:key="part.key"
					v-model="entry.address[part.key]"
					:class="{ 'sm:col-span-2': part.key === 'street' || part.key === 'country' }"
					:maxlength="CONFIG_ADDRESS_PART_MAX"
					:placeholder="$t(part.label)"
					:aria-label="$t(part.label)"
					:data-testid="part.testId"
				/>
			</div>
			<Input
				v-else-if="entry.type !== 'address'"
				v-model="entry.value"
				:type="INPUT_TYPES[entry.type]"
				class="col-span-2 md:col-span-1"
				:maxlength="CONFIG_VALUE_MAX"
				:placeholder="valuePlaceholder(entry.type, t)"
				:aria-label="$t('admin.config.entryValue')"
				data-testid="config-entry-value"
			/>

			<div class="col-span-2 flex items-center justify-end gap-2 md:col-span-1 md:pt-2">
				<Button
					variant="ghost"
					text="secondary"
					size="sm"
					square
					:disabled="first"
					:aria-label="$t('admin.moveUp')"
					@click="emit('move', -1)"
				>
					&#8593;
				</Button>
				<Button
					variant="ghost"
					text="secondary"
					size="sm"
					square
					:disabled="last"
					:aria-label="$t('admin.moveDown')"
					@click="emit('move', 1)"
				>
					&#8595;
				</Button>
				<Button
					variant="ghost"
					text="danger"
					size="sm"
					square
					:disabled="required"
					:title="required ? $t('admin.config.requiredEntry') : undefined"
					:aria-label="$t('admin.config.removeEntry')"
					data-testid="config-entry-remove"
					@click="emit('remove')"
				>
					&#10005;
				</Button>
			</div>
		</div>
		<p
			v-if="error"
			class="m-0 text-xs text-red-600"
		>
			{{ error }}
		</p>
	</li>
</template>
