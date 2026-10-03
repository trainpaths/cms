import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { getApiPublicInstance } from '../api/sdk.gen'
import type { InstanceConfig } from '../api/types.gen'

/**
 * What the admin needs from the instance config (`cms.config.json`, read by the API): public auth on/off, the
 * built-in blocks hidden from the inserters and the site config shape (fields + groups) the Configuration form
 * renders. Loaded once by `createAdmin()` before the first route guard.
 */
export const useInstanceStore = defineStore('instance', () => {
	// until loaded / on failure: the API's own defaults
	const config = ref<InstanceConfig>({
		publicAuth: false,
		excludedBlocks: [],
		siteConfig: { fields: [], groups: [] },
	})

	async function load(): Promise<void> {
		try {
			config.value = (await getApiPublicInstance()).data
		} catch {
			// keep the defaults; the API enforces the real values anyway
		}
	}

	// built-ins hidden by the instance config (blocks.exclude) aren't offered in the inserters; existing ones still
	// render and edit
	const excluded = computed(() => new Set(config.value.excludedBlocks))
	const isOffered = (blockName: string) => !excluded.value.has(blockName)

	return { config, load, isOffered }
})
