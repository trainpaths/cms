<script setup lang="ts">
import { ref, watch } from 'vue'
import type { BlockInstance } from '../core/types'
import MediaActions from './MediaActions.vue'
import { useMediaImage } from './useMediaImage'
import { assignMedia } from './assignMedia'
import { useMediaStore } from '../../../stores/media'
import { useEditorStore } from '../../../stores/editor'
import { MEDIA_ALT_MAX } from '../limits'
import { errorMessage } from '../../../api-error'

/** Settings-panel image picker: preview, upload / choose / remove, and the media object's alt text. */
const props = defineProps<{ block: BlockInstance }>()

const media = useMediaStore()
const editor = useEditorStore()
const { asset, src, missing } = useMediaImage(() => props.block)

// Alt belongs to the media object: edited locally, saved on change (not part of the page/undo history).
const alt = ref('')
watch(
	() => asset.value?.alt,
	(value) => (alt.value = value ?? ''),
	{ immediate: true },
)

async function saveAlt() {
	if (!asset.value || alt.value.trim() === asset.value.alt) return
	try {
		await media.update(asset.value.id, { alt: alt.value.trim() })
	} catch (err: unknown) {
		editor.errorMessage = errorMessage(err, 'Failed to save alt text')
	}
}
</script>

<template>
	<div class="flex flex-col gap-12">
		<img
			v-if="src"
			:src="src"
			alt=""
			class="max-h-160 w-full rounded border border-gray-200 bg-gray-50 object-contain"
		/>
		<p
			v-else-if="missing"
			class="m-0 text-xs text-red-600"
		>
			This image was deleted from the media library.
		</p>
		<MediaActions
			:replace="!!src"
			@select="assignMedia(block.id, $event.id)"
		/>
		<label
			v-if="asset"
			class="field-label"
		>
			Alt text
			<input
				v-model="alt"
				type="text"
				:maxlength="MEDIA_ALT_MAX"
				class="input-field"
				placeholder="Describe the image…"
				data-testid="media-alt"
				@change="saveAlt"
			/>
		</label>
		<button
			v-if="src || missing"
			type="button"
			class="self-start cursor-pointer border-none bg-transparent p-0 text-xs text-red-600 hover:underline"
			@click="assignMedia(block.id, '')"
		>
			Remove image
		</button>
	</div>
</template>
