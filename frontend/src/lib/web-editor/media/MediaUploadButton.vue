<script setup lang="ts">
import { ref } from 'vue'
import type { MediaItem } from '../core/types'
import { MEDIA_ACCEPT } from '../limits'
import { errorMessage } from '../../../api-error'
import { useMediaStore } from '../../../stores/media'

withDefaults(defineProps<{ label?: string; buttonClass?: string; multiple?: boolean }>(), {
	label: 'Upload new',
	multiple: false,
	buttonClass:
		'cursor-pointer rounded border-none bg-primary px-12 py-6 text-sm text-white hover:bg-primary-dark disabled:opacity-60',
})
const emit = defineEmits<{ uploaded: [item: MediaItem]; error: [message: string]; busy: [uploading: boolean] }>()

const media = useMediaStore()
const input = ref<HTMLInputElement | null>(null)
const uploading = ref(false)

async function onChange() {
	const files = Array.from(input.value?.files ?? [])
	if (input.value) input.value.value = ''
	if (!files.length) return
	uploading.value = true
	emit('busy', true)
	try {
		for (const file of files) emit('uploaded', await media.upload(file))
	} catch (err: unknown) {
		emit('error', errorMessage(err, 'Upload failed'))
	} finally {
		uploading.value = false
		emit('busy', false)
	}
}
</script>

<template>
	<button
		type="button"
		:class="buttonClass"
		:disabled="uploading"
		data-testid="media-upload"
		@click="input?.click()"
	>
		{{ uploading ? 'Uploading…' : label }}
	</button>
	<input
		ref="input"
		type="file"
		:accept="MEDIA_ACCEPT"
		:multiple="multiple"
		class="hidden"
		data-testid="media-upload-input"
		@change="onChange"
	/>
</template>
