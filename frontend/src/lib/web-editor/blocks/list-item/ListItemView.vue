<script setup lang="ts">
import type { BlockInstance } from '../../core/types'

defineProps<{ block: BlockInstance }>()
</script>

<template>
	<li class="px-8 py-4 text-gray-700">{{ block.attributes.text }}</li>
</template>
