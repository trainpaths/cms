<script setup lang="ts">
import { computed } from 'vue'
import type { BlockInstance } from '../../core/types'

const props = defineProps<{ block: BlockInstance }>()

const label = computed(() => (props.block.attributes.label as string) || '')
const url = computed(() => (props.block.attributes.url as string) || '#')
const colorClass = computed(() => (props.block.attributes.textColor ? '' : 'text-primary'))
</script>

<template>
	<a
		:href="url"
		class="inline-block p-16 hover:underline"
		:class="colorClass"
		target="_blank"
		rel="noopener noreferrer"
	>
		{{ label || url }}
	</a>
</template>
