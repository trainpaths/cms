import { createRouter, START_LOCATION, type Router, type RouterHistory, type RouteComponent } from 'vue-router'
import type { Pinia } from 'pinia'
import { usePublicPageStore } from './store'
import { pageTitle, screenTitle } from './head'
import { SCREENS } from './screens'
import PublicPage from './PublicPage.vue'
import { useSiteConfigStore } from '../stores/siteConfig'

// lazy: plain visitors don't load the forms; router.isReady() resolves them before SSR and hydration
const screenViews: Record<string, () => Promise<RouteComponent>> = {
	login: () => import('./screens/Login.vue'),
	register: () => import('./screens/Register.vue'),
	'forgot-password': () => import('./screens/ForgotPassword.vue'),
	'reset-password': () => import('./screens/ResetPassword.vue'),
	'verify-email': () => import('./screens/VerifyEmail.vue'),
	'user-profile': () => import('./screens/UserProfile.vue'),
	'user-password': () => import('./screens/UserPassword.vue'),
}

/**
 * Public site router. Route names match the admin SPA (`home`, `public-page`) so the shared SiteHeader /
 * SiteFooter links work in both. The first navigation keeps the server-rendered state; later ones load
 * the page first, and anything the public site can't show (admin routes, unknown slugs, deeper paths)
 * becomes a full page load so the server decides. Customer screens (`screens/`) are client-side routes.
 */
export function createPublicRouter(history: RouterHistory, pinia: Pinia): Router {
	const screens = SCREENS.map((screen) => ({
		path: `/${screen.slug}`,
		name: screen.name,
		component: screenViews[screen.name]!,
		meta: { screen },
	}))
	const router = createRouter({
		history,
		routes: [
			{ path: '/', name: 'home', component: PublicPage },
			{ path: '/home', redirect: '/' },
			...screens,
			{ path: '/:slug', name: 'public-page', component: PublicPage },
			// deeper path: only reached on a not-found page the server rendered for it
			{ path: '/:pathMatch(.*)*', name: 'external', component: PublicPage },
		],
		scrollBehavior: (_to, _from, saved) => saved ?? { top: 0 },
	})

	router.beforeEach(async (to, from) => {
		if (from === START_LOCATION) {
			// the server sent something else for this screen path (not-found: public auth off): render that, same URL
			if (to.meta.screen && to.meta.screen.slug !== usePublicPageStore(pinia).slug)
				return { name: 'external', params: { pathMatch: to.path.slice(1).split('/') }, query: to.query, hash: to.hash }
			return true
		}
		if (to.meta.screen) {
			// login/register return to the page the visitor came from
			const remember = (to.name === 'login' || to.name === 'register') && !from.meta.screen
			if (remember && to.query.redirect === undefined)
				return { path: to.path, query: { ...to.query, redirect: from.fullPath }, hash: to.hash }
			return true
		}
		const store = usePublicPageStore(pinia)
		if (to.name !== 'external') {
			const slug = to.name === 'home' ? 'home' : String(to.params.slug)
			// same page (hash/query) or a page that exists
			if (slug === store.slug || (await store.load(slug))) return true
		}
		window.location.assign(to.fullPath)
		return false
	})

	router.afterEach((to, from) => {
		if (from === START_LOCATION) return
		const config = useSiteConfigStore(pinia).config
		document.title = to.meta.screen
			? screenTitle(to.meta.screen.title, config)
			: pageTitle({ page: usePublicPageStore(pinia).page, config })
	})

	return router
}
