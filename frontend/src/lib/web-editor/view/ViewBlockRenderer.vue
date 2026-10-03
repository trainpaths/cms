<script setup lang="ts">
import { computed } from 'vue'
import ViewBlockWrapper from './ViewBlockWrapper.vue'
import { getBlockView } from '../core/blockViews'
import type { BlockInstance } from '../core/types'

const props = defineProps<{ block: BlockInstance }>()

const view = computed(() => getBlockView(props.block.name))
</script>

<template>
	<ViewBlockWrapper :block="block">
		<component
			:is="view"
			v-if="view"
			:block="block"
		/>
		<div
			v-else
			class="rounded bg-yellow-50 p-8 text-sm text-yellow-700"
		>
			Unknown block type: {{ block.name }}
		</div>
	</ViewBlockWrapper>
</template>
