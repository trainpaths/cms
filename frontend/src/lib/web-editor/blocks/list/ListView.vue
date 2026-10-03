<script setup lang="ts">
import { computed } from 'vue'
import ViewBlockList from '../../view/ViewBlockList.vue'
import type { BlockInstance } from '../../core/types'

const props = defineProps<{ block: BlockInstance }>()

const ordered = computed(() => props.block.attributes.ordered as boolean)
</script>

<template>
	<component
		:is="ordered ? 'ol' : 'ul'"
		class="m-0 py-16 pr-16 pl-40"
		:class="ordered ? 'list-decimal' : 'list-disc'"
	>
		<ViewBlockList :blocks="block.innerBlocks" />
	</component>
</template>
