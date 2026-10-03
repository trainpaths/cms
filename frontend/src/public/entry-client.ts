import { createWebHistory } from 'vue-router'
import { client } from '../api/client.gen'
import { getApiPublicMenusByHandle, getApiPublicPagesBySlug, getApiPublicSiteConfig } from '../api/sdk.gen'
import { createPublicApp } from './createPublicApp'
import { headTags } from './head'
import type { PublicState } from './state'

function embeddedState(): PublicState | null {
	try {
		return JSON.parse(document.getElementById('__STATE__')?.textContent ?? '') as PublicState
	} catch {
		return null
	}
}

// failed request → null, like a missing page
const dataOrNull = <T>(request: Promise<{ data: T }>) =>
	request.then(
		(r) => r.data,
		() => null,
	)

/** Fallback shell (page not rendered yet, or dev without SSR): fetch what the server would have embedded. */
async function fetchState(): Promise<PublicState> {
	const slug = location.pathname.slice(1) || 'home'
	const [page, menu, config] = await Promise.all([
		dataOrNull(getApiPublicPagesBySlug({ path: { slug } })),
		dataOrNull(getApiPublicMenusByHandle({ path: { handle: 'main' } })),
		dataOrNull(getApiPublicSiteConfig()),
	])
	const state = { slug, page, menu, config, baseUrl: location.origin }
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
	const embedded = embeddedState()
	const state = embedded ?? (await fetchState())
	const { app, router } = createPublicApp(state, createWebHistory(), !!embedded)
	await router.isReady()
	app.mount('#app')
}
