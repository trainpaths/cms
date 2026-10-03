<script setup lang="ts">
import type { BlockInstance } from '@trainpaths/cms/site'
import { toneClass } from './tone'

defineProps<{ block: BlockInstance }>()
</script>

<template>
	<div class="p-16">
		<div
			class="rounded-md border-l-4 px-16 py-12 font-sans text-base whitespace-pre-line text-gray-800"
			:class="toneClass(block.attributes.tone)"
			data-testid="callout"
		>
			{{ block.attributes.text }}
		</div>
	</div>
</template>
