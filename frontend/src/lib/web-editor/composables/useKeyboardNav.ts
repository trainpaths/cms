import { onMounted, onUnmounted } from 'vue'
import { useSelection } from './useSelection'
import { useEditorStore } from '../../../stores/editor'

export function useKeyboardNav() {
	const store = useEditorStore()
	const { selectPrev, selectNext, selectParent, selectFirstChild, clear } = useSelection()

	function handleKeydown(e: KeyboardEvent) {
		if (e.ctrlKey || e.metaKey) {
			// Shift turns 'z' into 'Z'; Ctrl+Y is the Windows redo
			const key = e.key.toLowerCase()
			if (key === 'z' || key === 'y') {
				e.preventDefault()
				if (key === 'y' || e.shiftKey) {
					store.redo()
				} else {
					store.undo()
				}
				return
			}
		}

		const tag = (e.target as HTMLElement)?.tagName
		if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return

		switch (e.key) {
			case 'ArrowUp':
				e.preventDefault()
				selectPrev()
				break
			case 'ArrowDown':
				e.preventDefault()
				selectNext()
				break
			case 'ArrowLeft':
				e.preventDefault()
				selectParent()
				break
			case 'ArrowRight':
				e.preventDefault()
				selectFirstChild()
				break
			case 'Escape':
				clear()
				break
			case 'Delete':
			case 'Backspace':
				if (store.selectedBlockId) {
					e.preventDefault()
					const id = store.selectedBlockId
					selectPrev()
					store.removeBlock(id)
				}
				break
		}
	}

	onMounted(() => {
		document.addEventListener('keydown', handleKeydown)
	})

	onUnmounted(() => {
		document.removeEventListener('keydown', handleKeydown)
	})
}
