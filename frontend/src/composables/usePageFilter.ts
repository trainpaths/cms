import { computed, ref, toValue, type MaybeRefOrGetter } from 'vue'
import type { PageSummary } from '../lib/web-editor'

export type PageSort = 'updated-desc' | 'updated-asc'

/** Search (title/slug), tag filter and updatedAt sort over a page list; state for `PageFilters.vue`. */
export function usePageFilter(pages: MaybeRefOrGetter<PageSummary[]>) {
	const search = ref('')
	const tag = ref('')
	const sort = ref<PageSort>('updated-desc')

	const active = computed(() => !!search.value.trim() || !!tag.value)

	const filtered = computed(() => {
		const query = search.value.trim().toLowerCase()
		const dir = sort.value === 'updated-desc' ? -1 : 1
		return toValue(pages)
			.filter((p) => !tag.value || p.tags.includes(tag.value))
			.filter((p) => !query || p.title.toLowerCase().includes(query) || p.slug.includes(query))
			.sort((a, b) => dir * a.updatedAt.localeCompare(b.updatedAt))
	})

	function reset() {
		search.value = ''
		tag.value = ''
	}

	return { search, tag, sort, active, filtered, reset }
}
