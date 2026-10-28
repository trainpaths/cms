import { computed, createApp, createSSRApp } from 'vue'
import { createPinia } from 'pinia'
import type { RouterHistory } from 'vue-router'
import PublicApp from './PublicApp.vue'
import { createPublicRouter } from './router'
import { usePublicPageStore } from './store'
import { usePublicMenusStore } from '../stores/menus'
import { useSiteConfigStore } from '../stores/siteConfig'
import type { PublicState } from './state'
import { createCmsI18n, LANGUAGE_SWITCH, LANGUAGES, languageOf, rememberLanguage } from '../i18n'
import { localePath } from './locale'

/**
 * One public app instance (per request on the server), stores seeded from `state`. `switchLanguage` (browser):
 * shows `path` (the current route in another language) without a page load; without it the switch is a full page load.
 */
export function createPublicApp(
	state: PublicState,
	history: RouterHistory,
	ssr: boolean,
	switchLanguage?: (path: string) => void,
) {
	// ssr: server render or hydration; false: client-only render (fallback shell)
	const app = ssr ? createSSRApp(PublicApp) : createApp(PublicApp)
	const pinia = createPinia()
	app.use(pinia)
	const language = languageOf(state.lang)
	const i18n = createCmsI18n(language)
	app.use(i18n)

	usePublicPageStore(pinia).set(state.slug, state.page, language.code)
	usePublicMenusStore(pinia).menus = { main: state.menu }
	if (state.config) useSiteConfigStore(pinia).set(state.config)

	const router = createPublicRouter(history, pinia, i18n.global.t)
	app.use(router)
	app.provide(LANGUAGE_SWITCH, {
		current: computed(() => language),
		languages: LANGUAGES,
		change: (code) => {
			if (code === language.code) return
			rememberLanguage(code)
			const path = localePath(router.currentRoute.value.fullPath, code)
			if (switchLanguage) switchLanguage(path)
			else window.location.assign(path)
		},
	})
	return { app, router, pinia, i18n }
}
