import { createWebHistory } from 'vue-router'
import { client } from '../api/client.gen'
import { setupInterceptors } from '../interceptors'
import { getApiPublicState, getApiPublicStateBySlug } from '../api/sdk.gen'
import { createPublicApp } from './createPublicApp'
import { headTags } from './head'
import type { PublicState } from './state'
import { refreshSurroundings } from './refresh'
import { SCREENS } from './screens'
import { localeBase, stripLocale } from './locale'

function embeddedState(): PublicState | null {
	try {
		return JSON.parse(document.getElementById('__STATE__')?.textContent ?? '') as PublicState
	} catch {
		return null
	}
}

/** Fallback shell (page not rendered yet, or dev without SSR): fetch what the server would have embedded, in one call. */
async function fetchState(): Promise<PublicState> {
	const { code: lang, path } = stripLocale(location.pathname)
	const slug = path.slice(1) || 'home'
	// screens and deeper paths aren't pages (`user/profile`: no slash in the path parameter); menu + config only
	const pageless = slug.includes('/') || SCREENS.some((screen) => screen.slug === slug)
	const query = { lang }
	// failed request → like a missing page (page null → not-found)
	const fetched = await (pageless ? getApiPublicState({ query }) : getApiPublicStateBySlug({ path: { slug }, query })).then(
		(r) => r.data,
		() => null,
	)
	return {
		slug,
		page: fetched?.page ?? null,
		menu: fetched?.menu ?? null,
		config: fetched?.config ?? null,
		baseUrl: location.origin,
		lang,
	}
}

/**
 * Hydrates the server-rendered public page (or renders the fallback shell). The instance's `src/entry-client.ts`
 * imports its stylesheet, then calls this.
 */
export async function hydratePublic(): Promise<void> {
	client.setConfig({ baseUrl: '' })
	const embedded = embeddedState()
	const state = embedded ?? (await fetchState())
	const { app, router, pinia, i18n } = createPublicApp(state, createWebHistory(localeBase(state.lang)), !!embedded)
	// the shell's <head> is empty; same tags the server render puts there
	if (!embedded) document.head.insertAdjacentHTML('beforeend', headTags(state, i18n.global.t))
	// bearer + refresh for the account screens, same ApiError as the admin. Session gone → customer login (staff
	// calls here, the admin bar's logout, never refresh)
	setupInterceptors(() => {
		if (router.hasRoute('login'))
			void router.push({ name: 'login', query: { redirect: router.currentRoute.value.fullPath } })
	})
	await router.isReady()
	app.mount('#app')
	// stored HTML older than a menu/site config change (the API marks it while the quiet re-render runs): refetch
	// both after hydration (no mismatch); header/footer read the stores. The fallback shell fetched fresh data already.
	if (embedded && document.getElementById('__STATE__')?.hasAttribute('data-refresh')) void refreshSurroundings(pinia)
}
