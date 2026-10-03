import { createApp, createSSRApp } from 'vue'
import { createPinia } from 'pinia'
import type { RouterHistory } from 'vue-router'
import PublicApp from './PublicApp.vue'
import { createPublicRouter } from './router'
import { usePublicPageStore } from './store'
import { usePublicMenusStore } from '../stores/menus'
import { useSiteConfigStore } from '../stores/siteConfig'
import type { PublicState } from './state'

/** One public app instance (per request on the server), stores seeded from `state`. */
export function createPublicApp(state: PublicState, history: RouterHistory, ssr: boolean) {
	// ssr: server render or hydration; false: client-only render (fallback shell)
	const app = ssr ? createSSRApp(PublicApp) : createApp(PublicApp)
	const pinia = createPinia()
	app.use(pinia)

	usePublicPageStore(pinia).set(state.slug, state.page)
	usePublicMenusStore(pinia).menus = { main: state.menu }
	if (state.config) useSiteConfigStore(pinia).set(state.config)

	const router = createPublicRouter(history, pinia)
	app.use(router)
	return { app, router }
}
