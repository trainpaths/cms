<script setup lang="ts">
import { computed } from 'vue'
import type { BlockInstance } from '../core/types'

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

const blockStyles = computed(() => {
	const styles: Record<string, string> = {}
	const bg = props.block.attributes.backgroundColor as string
	const text = props.block.attributes.textColor as string
	if (bg) styles.backgroundColor = bg
	if (text) styles.color = text
	return styles
})
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
