import { ref } from 'vue'
import { getBlockType } from '../core/blockRegistry'
import type { BlockInstance } from '../core/types'
import { useEditorStore } from '../../../stores/editor'

/**
 * Shared state for the block being dragged. `dataTransfer.getData` is unreadable during `dragover`,
 * so drop targets read the dragged block from here to decide whether to accept it.
 * - `move`: an existing block, dragged by its toolbar handle
 * - `new`: a block type dragged from the inserter sidebar, created on drop
 */
export type DraggedBlock = { kind: 'move'; id: string; name: string } | { kind: 'new'; name: string }

const dragged = ref<DraggedBlock | null>(null)
/** Where the dragged block would land; shared so only one list (the innermost under the pointer) shows it. */
const dropTarget = ref<{ parentId: string | null; index: number } | null>(null)

function contains(block: BlockInstance, targetId: string): boolean {
	return block.innerBlocks.some((child) => child.id === targetId || contains(child, targetId))
}

export function useBlockDrag() {
	const store = useEditorStore()

	function startDrag(value: DraggedBlock) {
		dragged.value = value
	}

	function endDrag() {
		dragged.value = null
		dropTarget.value = null
	}

	/** Whether the dragged block may be dropped into `parentId`'s inner blocks (top level when unset). */
	function canDropInto(parentId: string | null): boolean {
		const d = dragged.value
		if (!d) return false

		const requiredParents = getBlockType(d.name)?.parent
		if (!parentId) return !requiredParents?.length

		const parent = store.findBlockById(parentId)
		if (!parent) return false
		if (requiredParents?.length && !requiredParents.includes(parent.name)) return false
		const allowed = getBlockType(parent.name)?.allowedBlocks
		if (allowed?.length && !allowed.includes(d.name)) return false

		// never into itself or its own descendants
		if (d.kind === 'move') {
			if (parentId === d.id) return false
			const self = store.findBlockById(d.id)
			if (self && contains(self, parentId)) return false
		}
		return true
	}

	return { dragged, dropTarget, startDrag, endDrag, canDropInto }
}
