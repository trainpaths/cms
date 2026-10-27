import { createApp, ref } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { client } from './api/client.gen'
import { setupInterceptors } from './interceptors'
import { useAuthStore } from './stores/auth'
import { useInstanceStore } from './stores/instance'
import {
	createCmsI18n,
	DEFAULT_LANGUAGE,
	LANGUAGE_SWITCH,
	LANGUAGES,
	langFromCookie,
	languageOf,
	loadAdminMessages,
	setStoreTranslate,
	rememberLanguage,
	type Language,
} from './i18n'

/**
 * Boots the admin SPA into `#app`. The instance's `src/main.ts` imports its stylesheet (which imports
 * `@trainpaths/cms/style.css`), then calls this.
 */
export async function createAdmin(): Promise<void> {
	// Same-origin /api (Vite proxy in dev, nginx in container) so SameSite=Strict refresh cookie is sent.
	client.setConfig({ baseUrl: '' })

	const app = createApp(App)
	app.use(createPinia())

	// the language the visitor last picked (LanguageDropdown, public site or admin), else the site's default
	const current = ref<Language>(langFromCookie(document.cookie) ?? DEFAULT_LANGUAGE)
	const i18n = createCmsI18n(current.value)
	app.use(i18n)
	setStoreTranslate((key, named) => i18n.global.t(key, named ?? {}))
	document.documentElement.lang = current.value.tag
	app.provide(LANGUAGE_SWITCH, {
		current,
		languages: LANGUAGES,
		async change(code) {
			const language = languageOf(code)
			await loadAdminMessages(i18n, language.code)
			i18n.global.locale.value = language.code
			current.value = language
			document.documentElement.lang = language.tag
			rememberLanguage(language.code)
		},
	})

	// Session (refresh cookie) + instance config + admin strings before the first route guard runs.
	await Promise.all([
		useAuthStore().ensureSession(),
		useInstanceStore().load(),
		loadAdminMessages(i18n, current.value.code),
	])

	app.use(router)
	setupInterceptors(() => router.push({ name: 'admin-login' }))
	app.mount('#app')
}
