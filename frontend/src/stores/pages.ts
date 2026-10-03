import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { BlockInstance, PageDetail, PageSummary } from '../lib/web-editor/core/types'
import { getApiPages, getApiPagesById } from '../api/sdk.gen'

/**
 * Client-side page cache (stale-while-revalidate). Views render cached data at once and refresh in
 * the background, so revisiting the list or reopening a page doesn't wait for the API.
 * Details are seeded by prefetch (hovering a row) and by the editor's loads/saves.
 */
export const usePagesStore = defineStore('pages', () => {
	const items = ref<PageSummary[]>([])
	const loaded = ref(false)
	// plain map, not reactive: entries are copied out, never rendered from directly
	const details = new Map<string, PageDetail>()
	const prefetching = new Map<string, Promise<void>>()

	function countBlocks(blocks: BlockInstance[]): number {
		return blocks.reduce((n, b) => n + 1 + countBlocks(b.innerBlocks), 0)
	}

	function toSummary({ blocks, media: _media, ...rest }: PageDetail): PageSummary {
		return { ...rest, blockCount: countBlocks(blocks) }
	}

	function copy(page: PageDetail): PageDetail {
		return JSON.parse(JSON.stringify(page))
	}

	/** Fetches the list; the cached one stays on screen meanwhile. Throws ApiError. */
	async function load(): Promise<void> {
		items.value = (await getApiPages()).data
		loaded.value = true
	}

	/** Cached detail (a copy the caller may mutate), or null. */
	function cached(id: string): PageDetail | null {
		const page = details.get(id)
		return page ? copy(page) : null
	}

	function remember(page: PageDetail): void {
		details.set(page.id, copy(page))
		const index = items.value.findIndex((p) => p.id === page.id)
		if (index >= 0) items.value[index] = toSummary(page)
	}

	/** Warms the detail cache; errors are ignored (the editor reports them on open). */
	function prefetch(id: string): Promise<void> {
		if (details.has(id)) return Promise.resolve()
		let pending = prefetching.get(id)
		if (!pending) {
			pending = getApiPagesById({ path: { id } })
				.then(({ data }) => remember(data), () => {})
				.finally(() => prefetching.delete(id))
			prefetching.set(id, pending)
		}
		return pending
	}

	function add(page: PageDetail): void {
		details.set(page.id, copy(page))
		items.value.unshift(toSummary(page))
	}

	function forget(id: string): void {
		details.delete(id)
		items.value = items.value.filter((p) => p.id !== id)
	}

	return { items, loaded, load, cached, remember, prefetch, add, forget }
})
