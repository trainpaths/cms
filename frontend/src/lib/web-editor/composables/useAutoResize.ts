import { onMounted, watch, nextTick, type Ref } from 'vue'

/** Grows a textarea to fit its content; re-measures whenever `value` changes. */
export function useAutoResize(el: Ref<HTMLTextAreaElement | null>, value: Ref<string>) {
	function resize() {
		const ta = el.value
		if (!ta) return
		ta.style.height = 'auto'
		ta.style.height = `${ta.scrollHeight}px`
	}

	onMounted(resize)
	watch(value, () => nextTick(resize))

	return { resize }
}
