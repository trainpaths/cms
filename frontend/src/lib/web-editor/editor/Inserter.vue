<script setup lang="ts">
import { computed } from 'vue'
import { getRegisteredBlockTypes, getBlockType, createBlockInstance } from '../core/blockRegistry'
import { useEditorStore } from '../../../stores/editor'
import { useInstanceStore } from '../../../stores/instance'

const props = defineProps<{ parentId?: string }>()
const store = useEditorStore()

const instance = useInstanceStore()

const blockTypes = computed(() => {
	const all = getRegisteredBlockTypes().filter((bt) => instance.isOffered(bt.name))
	if (!props.parentId) return all

	const parentBlock = store.findBlockById(props.parentId)
	if (!parentBlock) return all

	const parentType = getBlockType(parentBlock.name)
	if (!parentType?.allowedBlocks?.length) return all

	return all.filter((bt) => parentType.allowedBlocks!.includes(bt.name))
})

function insert(name: string) {
	const block = createBlockInstance(name)
	store.addBlock(block, props.parentId)
}
</script>

<template>
	<div class="mt-8 flex gap-6">
		<button
			v-for="bt in blockTypes"
			:key="bt.name"
			class="cursor-pointer rounded border border-dashed border-gray-400 bg-transparent px-12 py-4 text-sm text-gray-500 hover:border-gray-500 hover:bg-gray-100 hover:text-gray-700"
			@click="insert(bt.name)"
		>
			+ {{ bt.title }}
		</button>
	</div>
</template>
