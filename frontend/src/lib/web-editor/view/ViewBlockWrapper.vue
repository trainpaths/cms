<script setup lang="ts">
import { computed } from 'vue'
import type { BlockInstance } from '../core/types'
import { blockColorStyle } from '../core/colors'

const props = defineProps<{ block: BlockInstance }>()

// overhang only when the window has room (mirrors BlockWrapper's canvas check), else no horizontal scroll on phones
const widthClass = computed(() => {
	const width = props.block.attributes.blockWidth as string
	switch (width) {
		case 'wide':
			return 'min-[784px]:-ml-32 min-[784px]:w-[calc(100%+64px)]'
		case 'full':
			return 'min-[816px]:-ml-48 min-[816px]:w-[calc(100%+96px)]'
		default:
			return ''
	}
})

const blockStyles = computed(() => blockColorStyle(props.block.attributes))
</script>

<template>
	<div
		class="rounded-lg"
		:class="widthClass"
		:style="blockStyles"
	>
		<slot />
	</div>
</template>
