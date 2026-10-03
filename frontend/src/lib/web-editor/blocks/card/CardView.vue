<script setup lang="ts">
import { computed } from 'vue'
import ViewBlockList from '../../view/ViewBlockList.vue'
import type { BlockInstance } from '../../core/types'
import { useMediaImage } from '../../media/useMediaImage'

const props = defineProps<{ block: BlockInstance }>()

const title = computed(() => (props.block.attributes.title as string) || '')
const description = computed(() => (props.block.attributes.description as string) || '')
const { src: imageSrc, alt: imageAlt } = useMediaImage(() => props.block)
const bgClass = computed(() => (props.block.attributes.backgroundColor ? '' : 'bg-white'))
const hasTextColor = computed(() => !!props.block.attributes.textColor)
</script>

<template>
	<div
		class="overflow-hidden rounded-lg shadow-xs @md:flex"
		:class="bgClass"
	>
		<img
			v-if="imageSrc"
			:src="imageSrc"
			:alt="imageAlt"
			class="w-full object-cover @md:w-2/5 @md:shrink-0"
		/>
		<div class="min-w-0 flex-1 p-16">
			<h3
				v-if="title"
				class="m-0 mb-8 text-lg font-semibold"
				:class="hasTextColor ? '' : 'text-gray-900'"
			>
				{{ title }}
			</h3>
			<p
				v-if="description"
				class="m-0 mb-12 text-sm whitespace-pre-line"
				:class="hasTextColor ? '' : 'text-gray-600'"
			>
				{{ description }}
			</p>
			<div
				v-if="block.innerBlocks.length"
				class="mt-12 flex flex-col gap-10"
			>
				<ViewBlockList :blocks="block.innerBlocks" />
			</div>
		</div>
	</div>
</template>
