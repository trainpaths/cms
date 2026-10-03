<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { nanoid } from 'nanoid'
import { Button, FormField, Input, Loading, UnsavedChangesDialog, useToast, useUnsavedChanges } from '@trainpaths/nb-ui'
import { getApiSiteConfig, putApiSiteConfig } from '../../api/sdk.gen'
import {
	CONFIG_MAX_ENTRIES,
	CONFIG_VALUE_MAX,
	errorMessage as describe,
	type ConfigEntryValue,
	type ConfigGroupDef,
	type ConfigPresetDef,
	type MediaRef,
	type SiteConfig,
} from '../../lib/web-editor'
import { emptyAddress } from '../../lib/siteConfig'
import ConfigImageField from '../../components/ConfigImageField.vue'
import ConfigEntryRow from '../../components/config/ConfigEntryRow.vue'
import { INPUT_TYPES, VALUE_PLACEHOLDERS } from '../../components/config/fieldInput'
import { useInstanceStore } from '../../stores/instance'
import { useSiteConfigStore } from '../../stores/siteConfig'

/**
 * The owner's site config, shaped by the instance config (`/api/public/instance` → siteConfig): fixed fields (firm
 * name...) and groups of entries (contact, socials...) the owner fills, adds, removes and reorders.
 */
const toast = useToast()
const siteConfig = useSiteConfigStore()
const schema = computed(() => useInstanceStore().config.siteConfig)

const fields = ref<Record<string, string>>({})
const groups = ref<Record<string, ConfigEntryValue[]>>({})
const logo = ref<MediaRef | null>(null)
const icon = ref<MediaRef | null>(null)
const shareImage = ref<MediaRef | null>(null)
const loaded = ref(false)
const loadFailed = ref(false)
const saving = ref(false)
const errorMessage = ref<string | null>(null)
const saved = ref('')

function snapshot(): string {
	const trim = (s: string) => s.trim()
	return JSON.stringify({
		fields: Object.fromEntries(Object.entries(fields.value).map(([k, v]) => [k, trim(v)])),
		groups: Object.fromEntries(
			Object.entries(groups.value).map(([k, entries]) => [
				k,
				entries.map((e) => ({ ...e, key: trim(e.key), value: trim(e.value) })),
			]),
		),
		logo: logo.value?.id ?? null,
		icon: icon.value?.id ?? null,
		shareImage: shareImage.value?.id ?? null,
	})
}

function apply(config: SiteConfig) {
	fields.value = { ...config.fields }
	groups.value = Object.fromEntries(
		Object.entries(config.groups).map(([k, entries]) => [
			k,
			entries.map(({ id, key, type, value, address }) => ({
				id,
				key,
				type,
				value,
				address: address ? { ...address } : null,
			})),
		]),
	)
	// the API sends every schema group; guard anyway so the add buttons always have a list to push to
	for (const group of schema.value.groups) groups.value[group.key] ??= []
	logo.value = config.logo ?? null
	icon.value = config.icon ?? null
	shareImage.value = config.shareImage ?? null
	saved.value = snapshot()
}

onMounted(async () => {
	try {
		apply((await getApiSiteConfig()).data)
		loaded.value = true
	} catch {
		loadFailed.value = true
	}
})

const dirty = computed(() => loaded.value && snapshot() !== saved.value)

/** Problems the API would reject anyway; shown inline, block saving. */
const fieldErrors = computed(() =>
	Object.fromEntries(
		schema.value.fields.map((f) => [f.key, f.required && !fields.value[f.key]?.trim() ? 'Required.' : '']),
	),
)
const entryErrors = computed(() =>
	Object.fromEntries(
		schema.value.groups.map((group) => {
			const seen = new Set<string>()
			const errors = (groups.value[group.key] ?? []).map((entry) => {
				const key = entry.key.trim().toLowerCase()
				if (!key) return 'Name is required.'
				if (seen.has(key)) return 'Name is used twice.'
				seen.add(key)
				return ''
			})
			return [group.key, errors]
		}),
	),
)
const valid = computed(
	() =>
		Object.values(fieldErrors.value).every((e) => !e) &&
		Object.values(entryErrors.value).every((errors) => errors.every((e) => !e)),
)

const presetOf = (group: ConfigGroupDef, key: string) => group.presets.find((p) => p.key === key)
const entriesOf = (group: ConfigGroupDef) => groups.value[group.key] ?? []
const unusedPresets = (group: ConfigGroupDef) =>
	group.presets.filter((p) => !entriesOf(group).some((e) => e.key === p.key))
const full = (group: ConfigGroupDef) => entriesOf(group).length >= CONFIG_MAX_ENTRIES

function addPreset(group: ConfigGroupDef, preset: ConfigPresetDef) {
	entriesOf(group).push({
		id: nanoid(10),
		key: preset.key,
		type: preset.type,
		value: '',
		address: preset.type === 'address' ? emptyAddress() : null,
	})
}

function addCustom(group: ConfigGroupDef) {
	entriesOf(group).push({ id: nanoid(10), key: '', type: 'text', value: '', address: null })
}

function move(group: ConfigGroupDef, index: number, delta: number) {
	const entries = entriesOf(group)
	const [entry] = entries.splice(index, 1)
	entries.splice(index + delta, 0, entry!)
}

/** Returns whether the config is saved afterwards (the unsaved-changes dialog continues only then). */
async function save(): Promise<boolean> {
	if (!valid.value || saving.value) return false
	saving.value = true
	errorMessage.value = null
	try {
		const { data: config } = await putApiSiteConfig({
			body: {
				fields: fields.value,
				groups: groups.value,
				logoMediaId: logo.value?.id ?? null,
				iconMediaId: icon.value?.id ?? null,
				shareImageMediaId: shareImage.value?.id ?? null,
			},
		})
		apply(config)
		siteConfig.set(config)
		toast.toast({ message: 'Configuration saved', type: 'success' })
		return true
	} catch (err: unknown) {
		errorMessage.value = describe(err, 'Failed to save configuration')
		return false
	} finally {
		saving.value = false
	}
}

const unsaved = useUnsavedChanges(dirty, save)
onBeforeRouteLeave(() => unsaved.confirmLeave())
</script>

<template>
	<div class="mx-auto max-w-5xl px-16 py-24 font-sans">
		<div class="mb-4 flex items-center justify-between gap-12">
			<h1 class="m-0 text-2xl text-gray-900">Configuration</h1>
			<div
				v-if="loaded"
				class="flex items-center gap-12"
			>
				<span
					v-if="dirty"
					class="text-xs text-gray-500"
					>Unsaved changes</span
				>
				<Button
					:loading="saving"
					:disabled="!dirty || !valid"
					data-testid="config-save"
					@click="save"
				>
					Save
				</Button>
			</div>
		</div>
		<p class="mb-16 text-xs text-gray-500">Information about your website, used on its public pages.</p>

		<div
			v-if="errorMessage"
			class="mb-16 flex items-center justify-between gap-12 rounded-md border border-red-200 bg-red-50 px-16 py-8 text-sm text-red-700"
			role="alert"
		>
			<span>{{ errorMessage }}</span>
			<button
				type="button"
				class="cursor-pointer border-none bg-transparent text-red-400 hover:text-red-600"
				aria-label="Dismiss"
				@click="errorMessage = null"
			>
				&#10005;
			</button>
		</div>

		<p
			v-if="loadFailed"
			class="text-sm text-red-600"
		>
			Failed to load configuration.
		</p>
		<Loading
			v-else-if="!loaded"
			label="Loading configuration…"
		/>

		<div
			v-else
			class="flex flex-col gap-16"
		>
			<section
				v-if="schema.fields.length"
				class="grid grid-cols-1 gap-16 rounded-lg border border-gray-200 bg-white p-16 shadow-xs md:grid-cols-2"
				data-testid="config-fields"
			>
				<FormField
					v-for="field in schema.fields"
					:key="field.key"
					:label="field.label"
					:required="field.required"
					:error="fieldErrors[field.key] || undefined"
				>
					<Input
						v-model="fields[field.key]"
						:type="field.type === 'address' ? 'text' : INPUT_TYPES[field.type]"
						:maxlength="CONFIG_VALUE_MAX"
						:placeholder="field.type === 'address' ? '' : VALUE_PLACEHOLDERS[field.type]"
						:data-testid="`config-field-${field.key}`"
					/>
				</FormField>
			</section>

			<section
				v-for="group in schema.groups"
				:key="group.key"
				class="rounded-lg border border-gray-200 bg-white p-16 shadow-xs"
				:data-testid="`config-group-${group.key}`"
			>
				<h2 class="m-0 mb-12 text-base font-semibold text-gray-900">{{ group.label }}</h2>
				<p
					v-if="!entriesOf(group).length"
					class="m-0 mb-12 text-sm text-gray-400"
				>
					No entries yet.
				</p>
				<ul class="m-0 flex list-none flex-col gap-12 p-0">
					<!-- the row edits the entry object in place (it's this list's element), no update event needed -->
					<ConfigEntryRow
						v-for="(entry, index) in entriesOf(group)"
						:key="entry.id"
						:model-value="entry"
						:preset="presetOf(group, entry.key)"
						:required="group.required.includes(entry.key)"
						:error="entryErrors[group.key]?.[index] ?? ''"
						:first="index === 0"
						:last="index === entriesOf(group).length - 1"
						@move="move(group, index, $event)"
						@remove="entriesOf(group).splice(index, 1)"
					/>
				</ul>
				<div class="mt-12 flex flex-wrap gap-8">
					<button
						v-for="preset in unusedPresets(group)"
						:key="preset.key"
						type="button"
						class="cursor-pointer rounded border border-dashed border-gray-300 bg-white px-12 py-6 text-sm text-gray-700 hover:border-primary hover:text-primary disabled:cursor-not-allowed disabled:opacity-60"
						:disabled="full(group)"
						:data-testid="`config-add-${preset.key}`"
						@click="addPreset(group, preset)"
					>
						+ {{ preset.label }}
					</button>
					<button
						v-if="group.allowCustom"
						type="button"
						class="cursor-pointer rounded border border-dashed border-gray-300 bg-white px-12 py-6 text-sm text-gray-700 hover:border-primary hover:text-primary disabled:cursor-not-allowed disabled:opacity-60"
						:disabled="full(group)"
						data-testid="config-add-custom"
						@click="addCustom(group)"
					>
						+ Custom entry
					</button>
				</div>
			</section>

			<div class="grid grid-cols-1 gap-16 md:grid-cols-3">
				<ConfigImageField
					v-model="logo"
					title="Logo"
					hint="Shown in the footer and navigation of the website. PNG/WebP work best."
					test-id="config-logo"
					@error="errorMessage = $event"
				/>
				<ConfigImageField
					v-model="icon"
					title="Icon"
					hint="Browser tab icon of the website. Square PNG."
					test-id="config-icon"
					@error="errorMessage = $event"
				/>
				<ConfigImageField
					v-model="shareImage"
					title="Share image"
					hint="Link previews (WhatsApp, Slack...) of pages without an image of their own. 1200×630 works best."
					test-id="config-share-image"
					@error="errorMessage = $event"
				/>
			</div>
		</div>
	</div>
	<UnsavedChangesDialog
		v-if="unsaved.prompting.value"
		:saving="saving"
		:can-save="valid"
		@save="unsaved.answer('save')"
		@discard="unsaved.answer('discard')"
		@cancel="unsaved.answer('cancel')"
	/>
</template>
