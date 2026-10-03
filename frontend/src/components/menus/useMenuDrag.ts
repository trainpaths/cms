import { ref } from 'vue'

/**
 * Shared drag state of the menu editor (module-level: the page picker starts drags, the tree accepts them).
 * `dataTransfer` is unreadable during `dragover`, so drop targets read the dragged thing from here.
 * - `move`: an existing item, dragged by its grip
 * - `page`: a page dragged from the picker, becomes a new item on drop
 */
export type MenuDragged = { kind: 'move'; id: string } | { kind: 'page'; pageId: string }

/** `slot` = position `index` in `parentId`'s children (root when null); `inside` = last child of `id`. */
export type MenuDropTarget = { kind: 'slot'; parentId: string | null; index: number } | { kind: 'inside'; id: string }

const dragged = ref<MenuDragged | null>(null)
const dropTarget = ref<MenuDropTarget | null>(null)

export function useMenuDrag() {
	function start(e: DragEvent, value: MenuDragged, image?: HTMLElement | null) {
		dragged.value = value
		if (!e.dataTransfer) return
		e.dataTransfer.effectAllowed = value.kind === 'move' ? 'move' : 'copy'
		// Firefox only starts a drag with data set
		e.dataTransfer.setData('text/plain', value.kind === 'move' ? value.id : value.pageId)
		if (image) e.dataTransfer.setDragImage(image, 16, 16)
	}

	function end() {
		dragged.value = null
		dropTarget.value = null
	}

	const isSlot = (parentId: string | null, index: number) =>
		dropTarget.value?.kind === 'slot' && dropTarget.value.parentId === parentId && dropTarget.value.index === index

	const isInside = (id: string) => dropTarget.value?.kind === 'inside' && dropTarget.value.id === id

	return { dragged, dropTarget, start, end, isSlot, isInside }
}
