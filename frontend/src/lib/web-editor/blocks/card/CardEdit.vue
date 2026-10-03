<script setup lang="ts">
import { ref, computed } from 'vue'
import BlockList from '../../editor/BlockList.vue'
import MediaActions from '../../media/MediaActions.vue'
import type { BlockInstance } from '../../core/types'
import { useSelection } from '../../composables/useSelection'
import { useBlockAttribute } from '../../composables/useBlockAttribute'
import { useAutoResize } from '../../composables/useAutoResize'
import { useMediaImage } from '../../media/useMediaImage'
import { assignMedia } from '../../media/assignMedia'

const props = defineProps<{ block: BlockInstance }>()
const { isSelected } = useSelection()

const selected = computed(() => isSelected(props.block.id))

const title = useBlockAttribute(() => props.block, 'title', '')
const description = useBlockAttribute(() => props.block, 'description', '')
const { src: imageSrc, alt: imageAlt, missing } = useMediaImage(() => props.block)

function choose(mediaId: string) {
	assignMedia(props.block.id, mediaId)
}

// chosen colours come from the wrapper; defaults only when unset
const bgClass = computed(() => (props.block.attributes.backgroundColor ? '' : 'bg-white'))
const hasTextColor = computed(() => !!props.block.attributes.textColor)

const descriptionEl = ref<HTMLTextAreaElement | null>(null)
useAutoResize(descriptionEl, description)
</script>

<template>
	<!-- no overflow-hidden: would clip child blocks' floating toolbars -->
	<div
		class="rounded-lg shadow-xs @md:flex"
		:class="bgClass"
	>
		<!-- @md: image left, text right (container query: canvas width in editor, page width in view) -->
		<div class="relative @md:w-2/5 @md:shrink-0">
			<img
				v-if="imageSrc"
				:src="imageSrc"
				:alt="imageAlt"
				class="w-full rounded-t-lg object-cover @md:h-full @md:rounded-l-lg @md:rounded-tr-none"
			/>
			<div
				v-else-if="selected"
				class="flex flex-col items-center gap-8 rounded-t-lg bg-gray-50 py-24 text-gray-400 @md:h-full @md:justify-center @md:rounded-l-lg @md:rounded-tr-none"
			>
				<span class="text-2xl">🖼</span>
				<span class="text-xs">
					{{ missing ? 'This image was deleted from the media library' : 'Add an image (optional)' }}
				</span>
				<MediaActions @select="choose($event.id)" />
			</div>
			<div
				v-if="selected && imageSrc"
				class="absolute left-8 top-8"
			>
				<div class="rounded-md bg-gray-900 px-8 py-6 shadow-lg">
					<MediaActions
						dark
						replace
						@select="choose($event.id)"
					/>
				</div>
			</div>
		</div>

		<div class="min-w-0 flex-1 p-16">
			<input
				v-model="title"
				type="text"
				class="m-0 mb-8 w-full border-none bg-transparent p-0 text-lg font-semibold outline-hidden placeholder:font-normal placeholder:italic placeholder:text-gray-400 focus:ring-0"
				:class="hasTextColor ? '' : 'text-gray-900'"
				placeholder="Click to add title..."
			/>

			<textarea
				ref="descriptionEl"
				v-model="description"
				rows="1"
				class="m-0 mb-12 block w-full resize-none overflow-hidden border-none bg-transparent p-0 text-sm outline-hidden placeholder:italic placeholder:text-gray-400 focus:ring-0"
				:class="hasTextColor ? '' : 'text-gray-600'"
				placeholder="Add description..."
			/>

			<div
				v-if="block.innerBlocks.length || selected"
				class="inner-blocks-container mt-12 flex flex-col gap-4"
			>
				<span class="inner-blocks-label">Content</span>
				<BlockList
					:blocks="block.innerBlocks"
					:parent-id="block.id"
				/>
			</div>
		</div>
	</div>
</template>
