import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { PublicPage } from '../lib/web-editor'
import { getApiPublicPagesBySlug } from '../api/sdk.gen'
import { DEFAULT_LANGUAGE } from '../i18n/languages'

/** Current public page (in the app's language). Seeded from the embedded state; client navigation loads the next one. */
export const usePublicPageStore = defineStore('publicPage', () => {
	const slug = ref('')
	const page = ref<PublicPage | null>(null)
	const lang = ref(DEFAULT_LANGUAGE.code)

	function set(nextSlug: string, next: PublicPage | null, nextLang?: string): void {
		slug.value = nextSlug
		page.value = next
		if (nextLang) lang.value = nextLang
	}

	/** false when the page doesn't exist (or the request failed): the caller hands over to the server. */
	async function load(nextSlug: string): Promise<boolean> {
		try {
			set(nextSlug, (await getApiPublicPagesBySlug({ path: { slug: nextSlug }, query: { lang: lang.value } })).data)
			return true
		} catch {
			return false
		}
	}

	return { slug, page, lang, set, load }
})
