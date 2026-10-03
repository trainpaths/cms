<script setup lang="ts">
import { computed } from 'vue'
import type { BlockInstance } from '../../core/types'

const props = defineProps<{ block: BlockInstance }>()

const text = computed(() => (props.block.attributes.text as string) || '')
const alignment = computed(() => (props.block.attributes.alignment as string) || 'left')
const alignClasses: Record<string, string> = { left: 'text-left', center: 'text-center', right: 'text-right' }
const colorClass = computed(() => (props.block.attributes.textColor ? '' : 'text-gray-700'))
</script>

<template>
	<p
		:class="[alignClasses[alignment], colorClass]"
		class="p-16 leading-relaxed whitespace-pre-line"
	>
		{{ text }}
	</p>
</template>
