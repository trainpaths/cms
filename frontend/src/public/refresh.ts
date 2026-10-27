import type { Pinia } from 'pinia'
import { getApiPublicState } from '../api/sdk.gen'
import { usePublicMenusStore } from '../stores/menus'
import { useSiteConfigStore } from '../stores/siteConfig'
import { usePublicPageStore } from './store'

/**
 * Stored HTML older than a menu/site config change (`data-refresh`): main menu + site config in one call, seeded
 * into the stores header/footer read. Keeps what the page was rendered with on failure. Never throws.
 */
export async function refreshSurroundings(pinia: Pinia): Promise<void> {
	try {
		const { data } = await getApiPublicState({ query: { lang: usePublicPageStore(pinia).lang } })
		usePublicMenusStore(pinia).menus.main = data.menu ?? null
		useSiteConfigStore(pinia).set(data.config)
	} catch {
		// keep what the page was rendered with
	}
}
