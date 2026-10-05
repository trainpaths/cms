<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { Loading, useConfirm } from '@trainpaths/nb-ui'
import {
	MediaGrid,
	MediaUploadButton,
	MEDIA_ALT_MAX,
	MEDIA_FILENAME_MAX,
	errorMessage as describe,
	useMediaStore,
	type MediaItem,
} from '../../lib/web-editor'

const media = useMediaStore()

const selectedId = ref<string | null>(null)
const selected = computed(() => media.items.find((m) => m.id === selectedId.value) ?? null)
const alt = ref('')
const fileName = ref('')
const saving = ref(false)
const errorMessage = ref<string | null>(null)

// cached library renders at once; the forced load refreshes it in the background
onMounted(async () => {
	try {
		await media.load(true)
	} catch {
		errorMessage.value = 'Failed to load media'
	}
})

watch(
	selected,
	(item) => {
		alt.value = item?.alt ?? ''
		fileName.value = item?.fileName ?? ''
	},
	{ immediate: true },
)

const dirty = computed(
	() =>
		!!selected.value &&
		(alt.value.trim() !== selected.value.alt || fileName.value.trim() !== selected.value.fileName),
)

function toggle(item: MediaItem) {
	selectedId.value = selectedId.value === item.id ? null : item.id
}

function onUploaded(item: MediaItem) {
	selectedId.value = item.id
}

async function save() {
	if (!selected.value || !fileName.value.trim()) return
	saving.value = true
	try {
		await media.update(selected.value.id, { alt: alt.value.trim(), fileName: fileName.value.trim() })
	} catch (err: unknown) {
		errorMessage.value = describe(err, 'Failed to save changes')
	} finally {
		saving.value = false
	}
}

const { confirm } = useConfirm()

async function remove(item: MediaItem) {
	const ok = await confirm({
		title: `Delete "${item.fileName}"?`,
		message: 'Pages using it will no longer show the image.',
		confirmText: 'Delete',
		danger: true,
	})
	if (!ok) return
	try {
		await media.remove(item.id)
		selectedId.value = null
	} catch (err: unknown) {
		errorMessage.value = describe(err, 'Failed to delete media')
	}
}

function formatSize(bytes: number) {
	return bytes < 1024 * 1024
		? `${Math.max(1, Math.round(bytes / 1024))} KB`
		: `${(bytes / 1024 / 1024).toFixed(1)} MB`
}
</script>

<template>
	<div
		v-if="errorMessage"
		class="fixed left-1/2 top-16 z-50 -translate-x-1/2 transform"
	>
		<div
			class="flex items-center gap-12 rounded-md border border-red-200 bg-red-50 px-16 py-8 text-sm text-red-700 shadow-lg"
		>
			<span>{{ errorMessage }}</span>
			<button
				class="cursor-pointer border-none bg-transparent text-red-400 hover:text-red-600"
				@click="errorMessage = null"
			>
				&#10005;
			</button>
		</div>
	</div>

	<div class="mx-auto max-w-5xl px-16 py-24 font-sans">
		<div class="mb-12 flex items-center justify-between">
			<h1 class="m-0 text-2xl text-gray-900">Media</h1>
			<MediaUploadButton
				label="+ Upload new"
				multiple
				@uploaded="onUploaded"
				@error="errorMessage = $event"
			/>
		</div>
		<p class="mb-16 text-xs text-gray-500">JPEG, PNG, GIF, WebP or AVIF, up to 10 MB.</p>

		<Loading
			v-if="!media.loaded && !errorMessage"
			label="Loading media…"
		/>
		<p
			v-else-if="media.loaded && !media.items.length"
			class="text-sm text-gray-400"
		>
			No media yet.
		</p>
		<MediaGrid
			:items="media.items"
			:selected-id="selectedId"
			grid-class="grid-cols-3 md:grid-cols-4 lg:grid-cols-6"
			@select="toggle"
		/>
	</div>

	<!-- below lg: dismissable modal; lg+: fixed far-right full-height sidebar, overlaying the page (grid never shifts) -->
	<div
		v-if="selected"
		class="fixed inset-0 z-40 flex items-center justify-center bg-black/40 p-16 font-sans lg:inset-auto lg:right-0 lg:top-0 lg:block lg:h-screen lg:w-280 lg:bg-transparent lg:p-0"
		@click.self="selectedId = null"
	>
		<aside
			class="scrollbar-thin flex max-h-full w-full max-w-400 flex-col gap-12 overflow-y-auto rounded-lg bg-white p-16 shadow-xl lg:h-full lg:max-w-none lg:rounded-none lg:border-l lg:border-gray-200 lg:bg-gray-50 lg:shadow-none"
			data-testid="media-details"
		>
			<div class="flex items-center justify-between gap-8">
				<h2 class="m-0 truncate text-sm font-semibold text-gray-900">{{ selected.fileName }}</h2>
				<button
					type="button"
					class="cursor-pointer rounded border-none bg-transparent px-4 py-2 text-gray-400 hover:text-gray-700"
					aria-label="Close"
					@click="selectedId = null"
				>
					&#10005;
				</button>
			</div>
			<img
				:src="selected.url"
				:alt="selected.alt"
				class="max-h-200 w-full rounded border border-gray-200 bg-white object-contain"
			/>
			<dl class="m-0 grid grid-cols-[auto_1fr] gap-x-8 gap-y-2 text-xs text-gray-600">
				<dt class="text-gray-400">Type</dt>
				<dd class="m-0">{{ selected.contentType }}</dd>
				<dt class="text-gray-400">Size</dt>
				<dd class="m-0">{{ formatSize(selected.size) }}</dd>
				<dt class="text-gray-400">Uploaded</dt>
				<dd class="m-0">{{ new Date(selected.createdAt).toLocaleDateString() }}</dd>
			</dl>
			<label class="flex flex-col gap-2 text-xs text-gray-500">
				Name
				<input
					v-model="fileName"
					type="text"
					:maxlength="MEDIA_FILENAME_MAX"
					class="rounded border border-gray-300 bg-white px-8 py-4 font-sans text-sm"
					data-testid="media-name"
					@keydown.enter="save"
				/>
			</label>
			<label class="flex flex-col gap-2 text-xs text-gray-500">
				Alt text
				<input
					v-model="alt"
					type="text"
					:maxlength="MEDIA_ALT_MAX"
					class="rounded border border-gray-300 bg-white px-8 py-4 font-sans text-sm"
					placeholder="Describe the image…"
					data-testid="media-alt"
				/>
			</label>
			<div class="flex items-center justify-between">
				<button
					class="cursor-pointer rounded border-none bg-primary px-12 py-6 text-sm text-white hover:bg-primary-dark disabled:opacity-60"
					:disabled="saving || !dirty || !fileName.trim()"
					data-testid="media-save"
					@click="save"
				>
					{{ saving ? 'Saving…' : 'Save' }}
				</button>
				<button
					class="cursor-pointer rounded border-none bg-transparent px-8 py-6 text-sm text-red-600 hover:bg-red-50"
					data-testid="media-delete"
					@click="remove(selected)"
				>
					Delete
				</button>
			</div>
		</aside>
	</div>
</template>
