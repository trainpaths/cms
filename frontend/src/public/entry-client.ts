import { createWebHistory } from 'vue-router'
import { client } from '../api/client.gen'
import { toApiError } from '../api-error'
import { getApiPublicStateBySlug } from '../api/sdk.gen'
import { createPublicApp } from './createPublicApp'
import { headTags } from './head'
import type { PublicState } from './state'
import { refreshSurroundings } from './refresh'

function embeddedState(): PublicState | null {
	try {
		return JSON.parse(document.getElementById('__STATE__')?.textContent ?? '') as PublicState
	} catch {
		return null
	}
}

/** Fallback shell (page not rendered yet, or dev without SSR): fetch what the server would have embedded, in one call. */
async function fetchState(): Promise<PublicState> {
	const slug = location.pathname.slice(1) || 'home'
	// failed request → like a missing page (page null → not-found)
	const fetched = await getApiPublicStateBySlug({ path: { slug } }).then(
		(r) => r.data,
		() => null,
	)
	const state = {
		slug,
		page: fetched?.page ?? null,
		menu: fetched?.menu ?? null,
		config: fetched?.config ?? null,
		baseUrl: location.origin,
	}
	// the shell's <head> is empty; same tags the server render puts there
	document.head.insertAdjacentHTML('beforeend', headTags(state))
	return state
}

/**
 * Hydrates the server-rendered public page (or renders the fallback shell). The instance's `src/entry-client.ts`
 * imports its stylesheet, then calls this.
 */
export async function hydratePublic(): Promise<void> {
	client.setConfig({ baseUrl: '' })
	// same ApiError as the admin (auth store logout from the admin bar)
	client.interceptors.error.use(toApiError)
	const embedded = embeddedState()
	const state = embedded ?? (await fetchState())
	const { app, router, pinia } = createPublicApp(state, createWebHistory(), !!embedded)
	await router.isReady()
	app.mount('#app')
	// stored HTML older than a menu/site config change (the API marks it while the quiet re-render runs): refetch
	// both after hydration (no mismatch); header/footer read the stores. The fallback shell fetched fresh data already.
	if (embedded && document.getElementById('__STATE__')?.hasAttribute('data-refresh')) void refreshSurroundings(pinia)
}
