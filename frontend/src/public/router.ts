import { createRouter, START_LOCATION, type Router, type RouterHistory } from 'vue-router'
import type { Pinia } from 'pinia'
import { usePublicPageStore } from './store'
import { pageTitle } from './head'
import { useSiteConfigStore } from '../stores/siteConfig'

// PublicApp renders everything from the store; routes exist for RouterLink + the URL
const Empty = { render: () => null }

/**
 * Public site router. Route names match the admin SPA (`home`, `public-page`) so the shared SiteHeader /
 * SiteFooter links work in both. The first navigation keeps the server-rendered state; later ones load
 * the page first, and anything the public site can't show (app routes like `/login`, unknown slugs,
 * deeper paths) becomes a full page load so the server decides.
 */
export function createPublicRouter(history: RouterHistory, pinia: Pinia): Router {
	const router = createRouter({
		history,
		routes: [
			{ path: '/', name: 'home', component: Empty },
			{ path: '/home', redirect: '/' },
			{ path: '/:slug', name: 'public-page', component: Empty },
			{ path: '/:pathMatch(.*)*', name: 'external', component: Empty },
		],
		scrollBehavior: (_to, _from, saved) => saved ?? { top: 0 },
	})

	router.beforeEach(async (to, from) => {
		if (from === START_LOCATION) return true
		const store = usePublicPageStore(pinia)
		if (to.name !== 'external') {
			const slug = to.name === 'home' ? 'home' : String(to.params.slug)
			// same page (hash/query) or a page that exists
			if (slug === store.slug || (await store.load(slug))) return true
		}
		window.location.assign(to.fullPath)
		return false
	})

	router.afterEach((_to, from) => {
		if (from !== START_LOCATION)
			document.title = pageTitle({ page: usePublicPageStore(pinia).page, config: useSiteConfigStore(pinia).config })
	})

	return router
}
