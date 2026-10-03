<script setup lang="ts">
import { computed, provide } from 'vue'
import { MENU_MAX_DEPTH, MENU_MAX_ITEMS, type MenuItem, type PageSummary } from '../../lib/web-editor'
import { canInsert, countItems, height, insertAt, locate, moveTo } from '../../lib/menus/tree'
import MenuList from './MenuList.vue'
import { menuTreeKey, pageItem, pagePath } from './context'
import { useMenuDrag, type MenuDropTarget } from './useMenuDrag'

/**
 * Editable menu item tree: rows with label + optional link, move buttons, and drag & drop of items and of
 * pages from the picker. Drop slots between rows show a line (no layout shift); dropping on a row's middle
 * nests inside it. Empty space below the tree appends at the root.
 */
const props = defineProps<{ pages: PageSummary[]; errors: Map<string, string> }>()
const items = defineModel<MenuItem[]>({ required: true })

const { dragged, dropTarget, end, isSlot } = useMenuDrag()

function accepts(target: MenuDropTarget): boolean {
	const d = dragged.value
	if (!d) return false
	const parentId = target.kind === 'slot' ? target.parentId : target.id
	if (d.kind === 'page') return countItems(items.value) < MENU_MAX_ITEMS && canInsert(items.value, parentId, 1, MENU_MAX_DEPTH)
	const moving = locate(items.value, d.id)
	return !!moving && canInsert(items.value, parentId, height(moving.item), MENU_MAX_DEPTH, d.id)
}

function drop() {
	const d = dragged.value
	const target = dropTarget.value
	const ok = !!target && accepts(target)
	end()
	if (!d || !target || !ok) return
	const parentId = target.kind === 'slot' ? target.parentId : target.id
	const index = target.kind === 'slot' ? target.index : (locate(items.value, target.id)?.item.children.length ?? 0)
	if (d.kind === 'move') {
		moveTo(items.value, d.id, parentId, index)
	} else {
		const page = props.pages.find((p) => p.id === d.pageId)
		if (page) insertAt(items.value, parentId, index, pageItem(page))
	}
}

provide(menuTreeKey, {
	root: items,
	pages: computed(() => new Map(props.pages.map((p) => [p.id, p]))),
	pagePaths: computed(() => new Map(props.pages.map((p) => [pagePath(p), p]))),
	errors: computed(() => props.errors),
	accepts,
	drop,
})

// space below the tree (and the empty state) = append at the root
function onDragOver(e: DragEvent) {
	const target: MenuDropTarget = { kind: 'slot', parentId: null, index: items.value.length }
	if (!accepts(target)) return
	e.preventDefault()
	dropTarget.value = target
}

function onDragLeave(e: DragEvent) {
	if (!(e.currentTarget as HTMLElement).contains(e.relatedTarget as Node | null)) dropTarget.value = null
}

function onDrop(e: DragEvent) {
	e.preventDefault()
	drop()
}
</script>

<template>
	<div
		class="pb-24"
		data-testid="menu-tree"
		@dragover="onDragOver"
		@dragleave="onDragLeave"
		@drop="onDrop"
	>
		<div
			v-if="!items.length"
			class="rounded-md border-2 border-dashed p-24 text-center text-sm"
			:class="isSlot(null, 0) ? 'border-primary bg-primary/5 text-primary' : 'border-gray-300 text-gray-500'"
		>
			No items yet. Add an item or drag pages here from the list.
		</div>
		<MenuList
			v-else
			:items="items"
			:parent-id="null"
			:depth="1"
		/>
	</div>
</template>
