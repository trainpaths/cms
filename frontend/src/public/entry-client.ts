import { createWebHistory, type Router, type RouterHistory } from 'vue-router'
import type { App } from 'vue'
import { client } from '../api/client.gen'
import { setupInterceptors } from '../interceptors'
import { getApiPublicState, getApiPublicStateBySlug } from '../api/sdk.gen'
import { createPublicApp } from './createPublicApp'
import { headTags, pageTitle, screenTitle } from './head'
import type { PublicState } from './state'
import { refreshSurroundings } from './refresh'
import { SCREENS, screenOf } from './screens'
import { localeBase, stripLocale } from './locale'
import { languageOf } from '../i18n'

function embeddedState(): PublicState | null {
	try {
		return JSON.parse(document.getElementById('__STATE__')?.textContent ?? '') as PublicState
	} catch {
		return null
	}
}

/**
 * What the server would have embedded for `pathname`, in one call: the fallback shell (page not rendered yet, or dev
 * without SSR) and the language switch.
 */
async function fetchState(pathname: string): Promise<PublicState> {
	const { code: lang, path } = stripLocale(pathname)
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

interface Mounted {
	app: App
	router: Router
	history: RouterHistory
	lang: string
}

let current: Mounted | null = null
// latest switch wins (double clicks, back + forward)
let switchToken = 0

/** App of `state` under its language prefix, initial navigation done, not mounted yet. */
async function build(state: PublicState, ssr: boolean) {
	const history = createWebHistory(localeBase(state.lang))
	const built = createPublicApp(state, history, ssr, (path) => void switchLanguage(path))
	await built.router.isReady()
	return { ...built, history }
}

/**
 * Shows `path` (another language's URL of the current page or screen) without a page load: fetches that language's
 * state, renders a fresh app under its prefix and swaps it in. `push` false: the URL is there already (back/forward).
 * Not-found or a failed request → full page load, the server decides.
 */
async function switchLanguage(path: string, push = true): Promise<void> {
	const token = ++switchToken
	const state = await fetchState(new URL(path, location.origin).pathname)
	if (token !== switchToken) return
	if (!state.page && !screenOf(state)) {
		if (push) location.assign(path)
		else location.reload()
		return
	}
	if (push) history.pushState(null, '', path)
	const next = await build(state, false)
	if (token !== switchToken) {
		next.history.destroy()
		return
	}
	// same task: no frame without content
	current?.app.unmount()
	current?.history.destroy()
	next.app.mount('#app')
	current = { app: next.app, router: next.router, history: next.history, lang: state.lang }
	const screen = screenOf(state)
	const t = next.i18n.global.t
	document.documentElement.lang = languageOf(state.lang).tag
	document.title = screen ? screenTitle(t(screen.title), state.config) : pageTitle(state, t)
}

/**
 * Hydrates the server-rendered public page (or renders the fallback shell). The instance's `src/entry-client.ts`
 * imports its stylesheet, then calls this.
 */
export async function hydratePublic(): Promise<void> {
	client.setConfig({ baseUrl: '' })
	// back/forward into another language: before vue-router's listener (registered with each history), which would
	// resolve the URL against the old prefix
	window.addEventListener('popstate', (event) => {
		if (!current || stripLocale(location.pathname).code === current.lang) return
		event.stopImmediatePropagation()
		void switchLanguage(location.pathname + location.search + location.hash, false)
	})
	// bearer + refresh for the account screens, same ApiError as the admin. Session gone → customer login (staff
	// calls here, the admin bar's logout, never refresh)
	setupInterceptors(() => {
		const active = current?.router
		if (active?.hasRoute('login'))
			void active.push({ name: 'login', query: { redirect: active.currentRoute.value.fullPath } })
	})
	const embedded = embeddedState()
	const state = embedded ?? (await fetchState(location.pathname))
	const { app, router, pinia, i18n, history } = await build(state, !!embedded)
	// the shell's <head> is empty; same tags the server render puts there
	if (!embedded) document.head.insertAdjacentHTML('beforeend', headTags(state, i18n.global.t))
	app.mount('#app')
	current = { app, router, history, lang: state.lang }
	// stored HTML older than a menu/site config change (the API marks it while the quiet re-render runs): refetch
	// both after hydration (no mismatch); header/footer read the stores. The fallback shell fetched fresh data already.
	if (embedded && document.getElementById('__STATE__')?.hasAttribute('data-refresh')) void refreshSurroundings(pinia)
}
