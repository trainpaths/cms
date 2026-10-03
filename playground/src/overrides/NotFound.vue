<script setup lang="ts">
// Override example: replaces the package's NotFound.vue everywhere (public 404 + admin catch-all), wrapping the original.
import Base from '@trainpaths/cms/src/views/NotFound.vue'
</script>

<template>
	<Base />
	<p
		class="mb-48 text-center font-sans text-sm text-gray-500"
		data-testid="playground-not-found"
	>
		Lost? The main menu above lists every page.
	</p>
</template>
