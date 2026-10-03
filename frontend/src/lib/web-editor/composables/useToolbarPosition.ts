import { ref, watch, nextTick, onMounted, onUnmounted, type Ref } from 'vue'

export function useToolbarPosition(
	wrapperRef: Ref<HTMLElement | null>,
	toolbarRef: Ref<HTMLElement | null>,
	isVisible: Ref<boolean>,
) {
	const positionClass = ref<'-top-40' | '-bottom-40'>('-top-40')

	function recalculate() {
		const wrapper = wrapperRef.value
		const toolbar = toolbarRef.value
		if (!wrapper || !toolbar) return

		const scrollContainer = wrapper.closest('main')
		if (!scrollContainer) return

		const containerRect = scrollContainer.getBoundingClientRect()
		const wrapperRect = wrapper.getBoundingClientRect()
		const toolbarHeight = 40 // matches the -top-40/-bottom-40 offset

		const spaceAbove = wrapperRect.top - containerRect.top
		const spaceBelow = containerRect.bottom - wrapperRect.bottom

		// prefer top, flip down if no room
		if (spaceAbove < toolbarHeight && spaceBelow >= toolbarHeight) {
			positionClass.value = '-bottom-40'
		} else {
			positionClass.value = '-top-40'
		}
	}

	let rafId: number | null = null
	function throttledRecalculate() {
		if (rafId !== null) return
		rafId = requestAnimationFrame(() => {
			recalculate()
			rafId = null
		})
	}

	let scrollContainer: HTMLElement | null = null

	function attachScrollListener() {
		const wrapper = wrapperRef.value
		if (!wrapper) return

		scrollContainer = wrapper.closest('main')
		if (scrollContainer) {
			scrollContainer.addEventListener('scroll', throttledRecalculate, { passive: true })
		}
	}

	function detachScrollListener() {
		if (scrollContainer) {
			scrollContainer.removeEventListener('scroll', throttledRecalculate)
			scrollContainer = null
		}
		if (rafId !== null) {
			cancelAnimationFrame(rafId)
			rafId = null
		}
	}

	watch(isVisible, (visible) => {
		if (visible) {
			nextTick(recalculate)
		}
	})

	onMounted(() => {
		attachScrollListener()
		if (isVisible.value) {
			recalculate()
		}
	})

	onUnmounted(() => {
		detachScrollListener()
	})

	return {
		positionClass,
		recalculate,
	}
}
