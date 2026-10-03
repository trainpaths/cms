import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import type { TagUsage } from '../lib/web-editor/core/types'
import { getApiTags } from '../api/sdk.gen'

/**
 * Tags in use (most used first): autocomplete, the "popular" row and the tag filter. Fetched once per session;
 * tag writes call `refresh()` since they change the counts.
 */
export const useTagsStore = defineStore('tags', () => {
	const usage = ref<TagUsage[]>([])
	let loaded = false
	let pending: Promise<void> | null = null

	const names = computed(() => usage.value.map((t) => t.name))
	const popular = computed(() => names.value.slice(0, 3))

	/** First call fetches, later calls reuse the result. Never throws. */
	function load(): Promise<void> {
		return loaded ? Promise.resolve() : refresh()
	}

	/** Refetches; concurrent calls share one request. Never throws. */
	function refresh(): Promise<void> {
		pending ??= getApiTags()
			.then(({ data }) => {
				usage.value = data
				loaded = true
			})
			.catch(() => {})
			.finally(() => (pending = null))
		return pending
	}

	return { usage, names, popular, load, refresh }
})
