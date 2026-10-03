<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import InlineToolbar from '../../editor/InlineToolbar.vue'
import MediaActions from '../../media/MediaActions.vue'
import type { BlockInstance } from '../../core/types'
import { useSelection } from '../../composables/useSelection'
import { useBlockAttribute } from '../../composables/useBlockAttribute'
import { useMediaImage } from '../../media/useMediaImage'
import { assignMedia } from '../../media/assignMedia'
import { useImageWidth } from './width'

const props = defineProps<{ block: BlockInstance }>()
const { isSelected } = useSelection()

const selected = computed(() => isSelected(props.block.id))

const { src, alt, missing } = useMediaImage(() => props.block)
const caption = useBlockAttribute(() => props.block, 'caption', '')
const width = useImageWidth(() => props.block)

const imgError = ref(false)

watch(src, () => {
	imgError.value = false
})

function choose(mediaId: string) {
	assignMedia(props.block.id, mediaId)
}
</script>

<template>
	<div class="p-16">
		<InlineToolbar
			:show="selected"
			:block-id="block.id"
		>
			<div class="flex flex-wrap items-center gap-8">
				<MediaActions
					dark
					:replace="!!src"
					@select="choose($event.id)"
				/>
				<div class="flex items-center gap-4">
					<span class="text-xs text-gray-400">W:</span>
					<input
						:value="width"
						type="number"
						min="1"
						max="100"
						class="toolbar-input w-64"
						aria-label="Width"
						@change="width = Number(($event.target as HTMLInputElement).value)"
					/>
					<span class="text-xs text-gray-400">%</span>
				</div>
			</div>
		</InlineToolbar>

		<figure class="m-0">
			<template v-if="src">
				<img
					v-if="!imgError"
					:src="src"
					:alt="alt"
					:style="{ width: `${width}%` }"
					class="mx-auto block rounded"
					@error="imgError = true"
				/>
				<div
					v-else
					class="rounded bg-gray-100 px-16 py-32 text-center text-sm text-gray-400"
				>
					Image failed to load
				</div>
			</template>
			<div
				v-else
				class="flex flex-col items-center gap-8 rounded border-2 border-dashed border-gray-200 bg-gray-50 py-32 text-gray-400"
			>
				<span class="text-3xl">🖼</span>
				<span class="text-sm">
					{{ missing ? 'This image was deleted from the media library' : 'Add an image' }}
				</span>
				<MediaActions @select="choose($event.id)" />
			</div>
			<input
				v-model="caption"
				type="text"
				class="mt-8 w-full border-none bg-transparent p-0 text-center text-sm text-gray-500 outline-hidden placeholder:italic placeholder:text-gray-400 focus:ring-0"
				placeholder="Add caption..."
			/>
		</figure>
	</div>
</template>
