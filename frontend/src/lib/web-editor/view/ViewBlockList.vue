<script setup lang="ts">
import ViewBlockRenderer from './ViewBlockRenderer.vue'
import type { BlockInstance } from '../core/types'

defineProps<{ blocks: BlockInstance[] }>()
</script>

<template>
	<ViewBlockRenderer
		v-for="block in blocks"
		:key="block.id"
		:block="block"
	/>
</template>
