<script setup lang="ts">
import { computed } from 'vue'
import type { BlockInstance } from '../../core/types'
import { useMediaImage } from '../../media/useMediaImage'
import { widthPercent } from './width'

const props = defineProps<{ block: BlockInstance }>()

const { src, alt } = useMediaImage(() => props.block)
const caption = computed(() => (props.block.attributes.caption as string) || '')
const width = computed(() => `${widthPercent(props.block.attributes.width)}%`)
</script>

<template>
	<figure
		v-if="src"
		class="m-0 p-16"
	>
		<img
			:src="src"
			:alt="alt"
			:style="{ width }"
			class="mx-auto block rounded"
		/>
		<figcaption
			v-if="caption"
			class="mt-8 text-center text-sm text-gray-500"
		>
			{{ caption }}
		</figcaption>
	</figure>
</template>
