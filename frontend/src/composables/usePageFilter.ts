import { computed, ref, toValue, type MaybeRefOrGetter } from 'vue'
import type { PageSummary } from '../lib/web-editor'

export type PageSort = 'updated-desc' | 'updated-asc'
/** '' = any status */
export type PageStatusFilter = '' | 'published' | 'draft'

/** Search (title/slug), tag + status filters and updatedAt sort over a page list; state for `PageFilters.vue`. */
export function usePageFilter(pages: MaybeRefOrGetter<PageSummary[]>) {
	const search = ref('')
	const tag = ref('')
	const status = ref<PageStatusFilter>('')
	const sort = ref<PageSort>('updated-desc')

	const active = computed(() => !!search.value.trim() || !!tag.value || !!status.value)

	const filtered = computed(() => {
		const query = search.value.trim().toLowerCase()
		const dir = sort.value === 'updated-desc' ? -1 : 1
		return toValue(pages)
			.filter((p) => !tag.value || p.tags.includes(tag.value))
			.filter((p) => !status.value || p.status === status.value)
			.filter((p) => !query || p.title.toLowerCase().includes(query) || p.slug.includes(query))
			.sort((a, b) => dir * a.updatedAt.localeCompare(b.updatedAt))
	})

	function reset() {
		search.value = ''
		tag.value = ''
		status.value = ''
	}

	return { search, tag, status, sort, active, filtered, reset }
}
