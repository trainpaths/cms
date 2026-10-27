import { computed, createApp, createSSRApp } from 'vue'
import { createPinia } from 'pinia'
import type { RouterHistory } from 'vue-router'
import PublicApp from './PublicApp.vue'
import { createPublicRouter } from './router'
import { usePublicPageStore } from './store'
import { usePublicMenusStore } from '../stores/menus'
import { useSiteConfigStore } from '../stores/siteConfig'
import type { PublicState } from './state'
import { createCmsI18n, DEFAULT_LANGUAGE, LANGUAGE_SWITCH } from '../i18n'

/** One public app instance (per request on the server), stores seeded from `state`. */
export function createPublicApp(state: PublicState, history: RouterHistory, ssr: boolean) {
	// ssr: server render or hydration; false: client-only render (fallback shell)
	const app = ssr ? createSSRApp(PublicApp) : createApp(PublicApp)
	const pinia = createPinia()
	app.use(pinia)
	// one language until pages are rendered per language: the instance's default
	const language = DEFAULT_LANGUAGE
	const i18n = createCmsI18n(language)
	app.use(i18n)
	app.provide(LANGUAGE_SWITCH, { current: computed(() => language), languages: [language], change: () => {} })

	usePublicPageStore(pinia).set(state.slug, state.page)
	usePublicMenusStore(pinia).menus = { main: state.menu }
	if (state.config) useSiteConfigStore(pinia).set(state.config)

	const router = createPublicRouter(history, pinia, i18n.global.t)
	app.use(router)
	return { app, router, pinia, i18n }
}
