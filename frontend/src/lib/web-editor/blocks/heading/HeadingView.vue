<script setup lang="ts">
import { computed } from 'vue'
import type { BlockInstance } from '../../core/types'

const props = defineProps<{ block: BlockInstance }>()

const level = computed(() => (props.block.attributes.level as number) || 2)
const text = computed(() => (props.block.attributes.text as string) || '')

const headingSizes: Record<number, string> = {
	1: 'text-4xl',
	2: 'text-3xl',
	3: 'text-2xl',
	4: 'text-xl',
	5: 'text-lg',
	6: 'text-base',
}
const colorClass = computed(() => (props.block.attributes.textColor ? '' : 'text-gray-900'))
</script>

<template>
	<component
		:is="`h${level}`"
		class="m-0 p-16 font-bold"
		:class="[headingSizes[level], colorClass]"
		>{{ text }}</component
	>
</template>
