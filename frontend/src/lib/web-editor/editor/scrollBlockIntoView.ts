import { nextTick } from 'vue'

/** Scrolls a canvas block to the middle of the viewport, after the DOM caught up (e.g. just inserted). */
export async function scrollBlockIntoView(id: string): Promise<void> {
	await nextTick()
	document.querySelector(`[data-block-id="${id}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
}
