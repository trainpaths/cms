<script setup lang="ts">
import type { MenuItem } from '../../lib/web-editor'
import MenuDropSlot from './MenuDropSlot.vue'
import MenuTreeItem from './MenuTreeItem.vue'
import { useMenuDrag } from './useMenuDrag'

/** One level of the tree: a drop slot before every row and one after the last. */
const props = defineProps<{ items: MenuItem[]; parentId: string | null; depth: number }>()
const { dropTarget } = useMenuDrag()

// nested lists: the indent area isn't a target (the tree wrapper would read it as "append to root")
function onDragOver(e: DragEvent) {
	if (!props.parentId) return
	e.stopPropagation()
	dropTarget.value = null
}
</script>

<template>
	<ul
		class="m-0 list-none p-0"
		@dragover="onDragOver"
	>
		<template
			v-for="(item, index) in items"
			:key="item.id"
		>
			<MenuDropSlot
				:parent-id="parentId"
				:index="index"
			/>
			<MenuTreeItem
				:item="item"
				:index="index"
				:siblings="items.length"
				:depth="depth"
				:parent-id="parentId"
			/>
		</template>
		<MenuDropSlot
			:parent-id="parentId"
			:index="items.length"
		/>
	</ul>
</template>
