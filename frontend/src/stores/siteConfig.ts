import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { SiteConfig } from '../lib/web-editor/core/types'
import { getApiPublicSiteConfig } from '../api/sdk.gen'

/**
 * Public site config (footer, favicon). Loaded once per session; the admin form pushes its saved
 * state via `set` so the preview reflects it without a reload.
 */
export const useSiteConfigStore = defineStore('siteConfig', () => {
	const config = ref<SiteConfig | null>(null)
	let pending: Promise<SiteConfig | null> | null = null

	/** Never throws: without a config the public site just has no footer / uses the CMS icon. */
	function load(): Promise<SiteConfig | null> {
		if (config.value) return Promise.resolve(config.value)
		pending ??= getApiPublicSiteConfig()
			.then(({ data }) => (config.value = data))
			.catch(() => null)
			.finally(() => (pending = null))
		return pending
	}

	function set(value: SiteConfig): void {
		config.value = value
	}

	return { config, load, set }
})
