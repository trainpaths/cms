<script setup lang="ts">
import { ref } from 'vue'
defineProps<{
	title: string
	defaultOpen?: boolean
}>()

const isOpen = ref(true)
</script>

<template>
	<div class="border-b border-gray-200 py-12">
		<button
			class="flex w-full cursor-pointer items-center justify-between border-none bg-transparent p-0 text-left"
			@click="isOpen = !isOpen"
		>
			<span class="text-xs font-semibold uppercase tracking-wide text-gray-500">{{ title }}</span>
			<span class="text-gray-400">{{ isOpen ? '−' : '+' }}</span>
		</button>
		<div
			v-show="isOpen"
			class="mt-12"
		>
			<slot />
		</div>
	</div>
</template>
