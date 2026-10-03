<script setup lang="ts">
import { computed } from 'vue'
import type { BlockInstance } from '../../core/types'
import { useEditorStore } from '../../../../stores/editor'
import { useSelection } from '../../composables/useSelection'

const props = defineProps<{ block: BlockInstance }>()
const store = useEditorStore()
const { isSelected } = useSelection()

const selected = computed(() => isSelected(props.block.id))

const text = computed({
	get: () => (props.block.attributes.text as string) || '',
	set: (value: string) => store.updateBlockAttributes(props.block.id, { text: value }),
})

const context = computed(() => store.getBlockContext(props.block.id))
const parentBlock = computed(() => {
	const parentId = context.value?.parentId
	return parentId ? store.findBlockById(parentId) : null
})
const isOrdered = computed(() => (parentBlock.value?.attributes.ordered as boolean) ?? false)
const itemIndex = computed(() => (context.value?.index ?? 0) + 1)
const marker = computed(() => (isOrdered.value ? `${itemIndex.value}.` : '\u2022'))
</script>

<template>
	<div class="flex items-start gap-8 px-8 py-4">
		<span class="mt-2 min-w-16 text-right text-gray-400">{{ marker }}</span>
		<span
			v-if="!selected"
			class="mt-2 flex-1 rounded border border-transparent px-8 py-4 text-gray-700"
			:class="{ 'text-gray-400 italic': !text }"
		>
			{{ text || 'Click to add item...' }}
		</span>
		<input
			v-else
			v-model="text"
			type="text"
			class="input-field flex-1"
			placeholder="List item text..."
		/>
	</div>
</template>
