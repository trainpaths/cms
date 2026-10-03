<script setup lang="ts">
// `defer`: a freshly inserted block is selected in the same tick it mounts, before its wrapper
// (which owns the #inline-toolbar-* target) is attached to the document.
defineProps<{ show: boolean; blockId: string }>()
</script>

<template>
	<Teleport
		v-if="show"
		defer
		:to="`#inline-toolbar-${blockId}`"
	>
		<span class="mx-4 h-20 w-1 bg-gray-600"></span>
		<slot />
	</Teleport>
</template>
