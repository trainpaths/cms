<script setup lang="ts">
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import type { MediaItem } from '../core/types'
import MediaUploadButton from './MediaUploadButton.vue'
import MediaPickerDialog from './MediaPickerDialog.vue'
import { useEditorStore } from '../../../stores/editor'

/**
 * "Upload new" + "Choose existing" for image blocks (and the site config logo/icon). With `replace` (an image
 * is already set) both sit behind a single "Replace" menu. `dark` for the inline toolbar. Upload errors go
 * to `onError`, by default the editor's error banner.
 */
const props = withDefaults(defineProps<{ dark?: boolean; replace?: boolean; onError?: (message: string) => void }>(), {
	dark: false,
	replace: false,
	// lazy: outside the editor the store is never touched
	onError: (message: string) => (useEditorStore().errorMessage = message),
})
const emit = defineEmits<{ select: [item: MediaItem] }>()

const pickerOpen = ref(false)
const menuOpen = ref(false)
const uploading = ref(false)
const root = ref<HTMLElement | null>(null)

const buttonClass = computed(() =>
	props.dark
		? 'cursor-pointer rounded border-none bg-gray-700 px-10 py-4 text-xs text-white hover:bg-gray-600 disabled:opacity-60'
		: 'cursor-pointer rounded border border-gray-300 bg-white px-10 py-4 text-xs text-gray-700 hover:bg-gray-50 disabled:opacity-60',
)
const menuItemClass = computed(() =>
	props.dark
		? 'w-full cursor-pointer whitespace-nowrap rounded border-none bg-transparent px-8 py-4 text-left text-xs text-gray-200 hover:bg-gray-700'
		: 'w-full cursor-pointer whitespace-nowrap rounded border-none bg-transparent px-8 py-4 text-left text-xs text-gray-700 hover:bg-gray-100',
)

function choose() {
	menuOpen.value = false
	pickerOpen.value = true
}

function onDocMouseDown(e: MouseEvent) {
	if (root.value && !root.value.contains(e.target as Node)) menuOpen.value = false
}

watch(menuOpen, (open) => {
	if (open) document.addEventListener('mousedown', onDocMouseDown)
	else document.removeEventListener('mousedown', onDocMouseDown)
})
onBeforeUnmount(() => document.removeEventListener('mousedown', onDocMouseDown))
</script>

<template>
	<div
		ref="root"
		class="relative flex items-center gap-6"
	>
		<button
			v-if="replace"
			type="button"
			:class="buttonClass"
			:disabled="uploading"
			aria-haspopup="menu"
			:aria-expanded="menuOpen"
			data-testid="media-replace"
			@click="menuOpen = !menuOpen"
		>
			{{ uploading ? 'Uploading…' : 'Replace' }}
		</button>
		<!-- v-show, not v-if: the upload button (and its file input) must stay mounted to emit after the menu closes -->
		<div
			v-show="!replace || menuOpen"
			:role="replace ? 'menu' : undefined"
			:class="
				replace
					? [
							'absolute left-0 top-full z-30 mt-4 flex min-w-full flex-col gap-2 rounded-md p-4 shadow-lg',
							dark ? 'bg-gray-900' : 'border border-gray-200 bg-white',
						]
					: 'flex items-center gap-6'
			"
		>
			<!-- wrapper: the upload button is a fragment (button + input), so no fallthrough @click -->
			<div
				class="contents"
				@click="menuOpen = false"
			>
				<MediaUploadButton
					:button-class="replace ? menuItemClass : buttonClass"
					@busy="uploading = $event"
					@uploaded="emit('select', $event)"
					@error="onError"
				/>
			</div>
			<button
				type="button"
				:class="replace ? menuItemClass : buttonClass"
				data-testid="media-choose"
				@click="choose"
			>
				Choose existing
			</button>
		</div>
		<MediaPickerDialog
			v-if="pickerOpen"
			@select="emit('select', $event)"
			@close="pickerOpen = false"
		/>
	</div>
</template>
