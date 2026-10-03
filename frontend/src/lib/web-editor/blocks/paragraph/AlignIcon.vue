<script setup lang="ts">
defineProps<{ align: 'left' | 'center' | 'right' }>()
</script>

<template>
	<svg
		xmlns="http://www.w3.org/2000/svg"
		width="16"
		height="16"
		viewBox="0 0 24 24"
		fill="none"
		stroke="currentColor"
		stroke-width="2"
		stroke-linecap="round"
		stroke-linejoin="round"
	>
		<template v-if="align === 'left'">
			<path d="M21 5H3" />
			<path d="M15 12H3" />
			<path d="M17 19H3" />
		</template>
		<template v-else-if="align === 'center'">
			<path d="M21 5H3" />
			<path d="M17 12H7" />
			<path d="M19 19H5" />
		</template>
		<template v-else>
			<path d="M21 5H3" />
			<path d="M21 12H9" />
			<path d="M21 19H7" />
		</template>
	</svg>
</template>
