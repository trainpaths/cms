<script setup lang="ts">
import { ref, watch, nextTick, provide } from 'vue'
import { useEditorStore } from '../../../stores/editor'
import BlockOutlineItem from './BlockOutlineItem.vue'
import { outlineKey } from './outline'

/**
 * Read-only tree of the page's blocks and their inner blocks. Clicking a row selects the block on the
 * canvas; selecting on the canvas expands the row's ancestors and scrolls the row into view.
 */
const store = useEditorStore()
const collapsed = ref(new Set<string>())
const list = ref<HTMLElement | null>(null)

function toggle(id: string) {
	if (!collapsed.value.delete(id)) collapsed.value.add(id)
}

provide(outlineKey, { collapsed, toggle })

watch(
	() => store.selectedBlockId,
	async (id) => {
		if (!id) return
		for (let ctx = store.getBlockContext(id); ctx?.parentId; ctx = store.getBlockContext(ctx.parentId))
			collapsed.value.delete(ctx.parentId)
		await nextTick()
		list.value?.querySelector(`[data-outline-id="${id}"]`)?.scrollIntoView({ block: 'nearest' })
	},
)
</script>

<template>
	<div
		ref="list"
		class="scrollbar-thin scrollbar-stable h-full overflow-y-auto p-8"
	>
		<ul
			v-if="store.blocks.length"
			class="m-0 list-none p-0"
		>
			<BlockOutlineItem
				v-for="block in store.blocks"
				:key="block.id"
				:block="block"
			/>
		</ul>
		<p
			v-else
			class="px-8 py-12 text-xs text-gray-400"
		>
			No blocks yet.
		</p>
	</div>
</template>
