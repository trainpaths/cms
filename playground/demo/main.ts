import '../src/style.css'
import { computed, createApp, ref } from 'vue'
import { createPinia } from 'pinia'
import { createRouter, createWebHashHistory } from 'vue-router'
import { useToast } from '@trainpaths/nb-ui'
import { client } from '@trainpaths/cms/src/api/client.gen.ts'
import { toApiError } from '@trainpaths/cms/src/api-error.ts'
import {
	createCmsI18n,
	DEFAULT_LANGUAGE,
	LANGUAGE_SWITCH,
	LANGUAGES,
	languageOf,
	loadAdminMessages,
	setStoreTranslate,
} from '@trainpaths/cms/src/i18n/index.ts'
import { useInstanceStore } from '@trainpaths/cms/src/stores/instance.ts'
import DemoApp from './DemoApp.vue'
import EditPage from './EditPage.vue'
import ViewPage from './ViewPage.vue'
import { DEMO, type DemoContext } from './context'
import { DEMO_MESSAGES } from './messages'
import { createMockFetch } from './mockApi'
import { createDemoStore } from './store'

/**
 * Browser-only demo (GitHub Pages): the real editor and public page view of one page, against an in-browser mock
 * of the API that keeps the page in localStorage. Two routes: `#/` (view) and `#/edit`.
 */
async function createDemo(): Promise<void> {
	const store = createDemoStore()
	const i18n = createCmsI18n(DEFAULT_LANGUAGE)
	// loadAdminMessages replaces a language's messages: add the demo's own strings after it
	async function loadMessages(code: string) {
		await loadAdminMessages(i18n, code)
		for (const c of new Set(['en', code])) if (DEMO_MESSAGES[c]) i18n.global.mergeLocaleMessage(c, DEMO_MESSAGES[c])
	}
	setStoreTranslate((key, named) => i18n.global.t(key, named ?? {}))

	client.setConfig({
		baseUrl: '',
		fetch: createMockFetch(store, {
			languages: LANGUAGES.map((l) => l.code),
			onStorageFull: () => useToast().toast.warning(i18n.global.t('demo.storageFull')),
		}),
	})
	client.interceptors.error.use(toApiError)

	// one language for the site view and the UI around it (the editor picks the edited language itself)
	const siteLang = ref(DEFAULT_LANGUAGE.code)
	const languageCodes = LANGUAGES.slice(1).map((l) => l.code)

	const router = createRouter({
		history: createWebHashHistory(),
		routes: [
			{ path: '/', name: 'home', component: ViewPage },
			{ path: '/edit', name: 'edit', component: EditPage },
			{ path: '/home', redirect: '/' },
			// the editor's view link puts the edited language first (`/de/home`): switch, then show the page
			...(languageCodes.length
				? [
						{
							path: `/:lang(${languageCodes.join('|')})/:slug?`,
							redirect: (to: { params: Record<string, unknown> }) => {
								void context.changeLanguage(String(to.params.lang))
								const slug = to.params.slug
								return !slug || slug === 'home' ? '/' : `/${String(slug)}`
							},
						},
					]
				: []),
			{ path: '/:slug', name: 'public-page', component: ViewPage },
			{ path: '/:pathMatch(.*)*', redirect: '/' },
		],
		scrollBehavior: () => ({ top: 0 }),
	})

	const context: DemoContext = {
		siteLang,
		async changeLanguage(code) {
			const language = languageOf(code)
			await loadMessages(language.code)
			i18n.global.locale.value = language.code
			siteLang.value = language.code
			document.documentElement.lang = language.tag
		},
		reset() {
			store.reset()
			// editor + page caches live in Pinia: start over from the seed
			window.location.hash = '#/'
			window.location.reload()
		},
	}

	const app = createApp(DemoApp)
	app.use(createPinia())
	app.use(i18n)
	app.provide(DEMO, context)
	app.provide(LANGUAGE_SWITCH, {
		current: computed(() => languageOf(siteLang.value)),
		languages: LANGUAGES,
		change: (code) => context.changeLanguage(code),
	})

	await Promise.all([useInstanceStore().load(), loadMessages(DEFAULT_LANGUAGE.code)])
	app.use(router)
	app.mount('#app')
}

void createDemo()
