import { computed } from 'vue'
import { useEditorStore } from '../../../stores/editor'
import type { BlockInstance } from '../core/types'

export function useSelection() {
	const store = useEditorStore()

	const selectedBlockId = computed(() => store.selectedBlockId)

	const selectedBlock = computed<BlockInstance | null>(() => {
		if (!store.selectedBlockId) return null
		return store.findBlockById(store.selectedBlockId)
	})

	function select(id: string): void {
		store.selectBlock(id)
	}

	function clear(): void {
		store.clearSelection()
	}

	function isSelected(id: string): boolean {
		return store.selectedBlockId === id
	}

	/** Build a flat depth-first ordered list of all block IDs */
	function flatOrder(): string[] {
		const ids: string[] = []
		function walk(list: BlockInstance[]) {
			for (const block of list) {
				ids.push(block.id)
				walk(block.innerBlocks)
			}
		}
		walk(store.blocks)
		return ids
	}

	function selectPrev(): void {
		const ids = flatOrder()
		if (!ids.length) return
		if (!store.selectedBlockId) {
			store.selectBlock(ids[ids.length - 1])
			return
		}
		const idx = ids.indexOf(store.selectedBlockId)
		if (idx > 0) store.selectBlock(ids[idx - 1])
	}

	function selectNext(): void {
		const ids = flatOrder()
		if (!ids.length) return
		if (!store.selectedBlockId) {
			store.selectBlock(ids[0])
			return
		}
		const idx = ids.indexOf(store.selectedBlockId)
		if (idx < ids.length - 1) store.selectBlock(ids[idx + 1])
	}

	function selectParent(): void {
		if (!store.selectedBlockId) return
		const ctx = store.getBlockContext(store.selectedBlockId)
		if (ctx?.parentId) store.selectBlock(ctx.parentId)
	}

	function selectFirstChild(): void {
		if (!store.selectedBlockId) return
		const block = store.findBlockById(store.selectedBlockId)
		if (block && block.innerBlocks.length > 0) {
			store.selectBlock(block.innerBlocks[0].id)
		}
	}

	return {
		selectedBlockId,
		selectedBlock,
		select,
		clear,
		isSelected,
		selectPrev,
		selectNext,
		selectParent,
		selectFirstChild,
	}
}
