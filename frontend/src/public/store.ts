import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { PublicPage } from '../lib/web-editor'
import { getApiPublicPagesBySlug } from '../api/sdk.gen'

/** Current public page. Seeded from the embedded state; client navigation loads the next one. */
export const usePublicPageStore = defineStore('publicPage', () => {
	const slug = ref('')
	const page = ref<PublicPage | null>(null)

	function set(nextSlug: string, next: PublicPage | null): void {
		slug.value = nextSlug
		page.value = next
	}

	/** false when the page doesn't exist (or the request failed): the caller hands over to the server. */
	async function load(nextSlug: string): Promise<boolean> {
		try {
			set(nextSlug, (await getApiPublicPagesBySlug({ path: { slug: nextSlug } })).data)
			return true
		} catch {
			return false
		}
	}

	return { slug, page, set, load }
})
