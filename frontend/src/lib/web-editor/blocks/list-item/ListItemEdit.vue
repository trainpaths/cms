<script setup lang="ts">
import { computed } from 'vue'
import type { BlockInstance } from '../../core/types'
import { useEditorStore } from '../../../../stores/editor'
import { useBlockText } from '../../composables/useBlockAttribute'

const props = defineProps<{ block: BlockInstance }>()
const store = useEditorStore()
const { value: text, original } = useBlockText(() => props.block, 'text')

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
		<input
			v-model="text"
			type="text"
			class="m-0 mt-2 min-w-0 flex-1 border-none bg-transparent px-8 py-4 text-gray-700 outline-hidden placeholder:italic placeholder:text-gray-400 focus:ring-0"
			:placeholder="original || $t('editor.listItem.placeholder')"
		/>
	</div>
</template>
