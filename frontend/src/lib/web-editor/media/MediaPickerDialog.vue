<script setup lang="ts">
import { ref, onMounted } from 'vue'
import type { MediaItem } from '../core/types'
import MediaGrid from './MediaGrid.vue'
import MediaUploadButton from './MediaUploadButton.vue'
import { useMediaStore } from '../../../stores/media'
import { Loading } from '@trainpaths/nb-ui'

const emit = defineEmits<{ select: [item: MediaItem]; close: [] }>()

const media = useMediaStore()
const error = ref<string | null>(null)
const dialog = ref<HTMLElement | null>(null)

onMounted(async () => {
	dialog.value?.focus()
	try {
		await media.load(true)
	} catch {
		error.value = 'Failed to load media'
	}
})

function onKeydown(e: KeyboardEvent) {
	e.stopPropagation()
	if (e.key === 'Escape') emit('close')
}

function pick(item: MediaItem) {
	emit('select', item)
	emit('close')
}
</script>

<template>
	<Teleport to="body">
		<!-- keydown stopped: editor's document-level shortcuts (Backspace deletes the block) must not fire -->
		<div
			class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-16 font-sans"
			@click.self="emit('close')"
			@keydown="onKeydown"
		>
			<div
				ref="dialog"
				role="dialog"
				aria-modal="true"
				aria-label="Choose media"
				tabindex="-1"
				class="flex max-h-[80vh] w-full max-w-3xl flex-col rounded-lg bg-white shadow-xl outline-hidden"
				data-testid="media-picker"
			>
				<div class="flex items-center justify-between border-b border-gray-200 px-16 py-12">
					<h2 class="m-0 text-base font-semibold text-gray-900">Choose media</h2>
					<div class="flex items-center gap-8">
						<MediaUploadButton
							@uploaded="pick"
							@error="error = $event"
						/>
						<button
							type="button"
							class="cursor-pointer rounded border-none bg-transparent px-8 py-4 text-gray-400 hover:text-gray-700"
							aria-label="Close"
							@click="emit('close')"
						>
							&#10005;
						</button>
					</div>
				</div>
				<p
					v-if="error"
					class="m-0 border-b border-red-200 bg-red-50 px-16 py-8 text-sm text-red-700"
				>
					{{ error }}
				</p>
				<div class="scrollbar-thin overflow-y-auto p-16">
					<Loading v-if="media.loading && !media.items.length" />
					<p
						v-else-if="!media.items.length"
						class="text-sm text-gray-400"
					>
						No media yet. Upload an image to get started.
					</p>
					<MediaGrid
						v-else
						:items="media.items"
						@select="pick"
					/>
				</div>
			</div>
		</div>
	</Teleport>
</template>
