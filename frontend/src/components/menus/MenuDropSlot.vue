<script setup lang="ts">
import { inject } from 'vue'
import { menuTreeKey } from './context'
import { useMenuDrag } from './useMenuDrag'

/**
 * Drop position `index` in `parentId`'s children. Always 10px tall (the gap between rows); the active line
 * grows 0 → 4px inside it, so showing it never shifts the layout (same as the block editor's BlockList).
 */
const props = defineProps<{ parentId: string | null; index: number }>()
const ctx = inject(menuTreeKey)!
const { dropTarget, isSlot } = useMenuDrag()

function onDragOver(e: DragEvent) {
	e.stopPropagation()
	const target = { kind: 'slot' as const, parentId: props.parentId, index: props.index }
	if (!ctx.accepts(target)) return
	e.preventDefault()
	dropTarget.value = target
}

function onDrop(e: DragEvent) {
	e.preventDefault()
	e.stopPropagation()
	ctx.drop()
}
</script>

<template>
	<li
		class="m-0 flex h-10 list-none items-center"
		aria-hidden="true"
		@dragover="onDragOver"
		@drop="onDrop"
	>
		<div
			class="w-full rounded-full transition-[height] duration-150 ease-out"
			:class="isSlot(parentId, index) ? 'h-4 bg-primary' : 'h-0'"
			:data-testid="isSlot(parentId, index) ? 'menu-drop-indicator' : undefined"
		/>
	</li>
</template>
