import { createRouter, createWebHistory, START_LOCATION, type NavigationGuard } from 'vue-router'
import { useAuthStore } from '../stores/auth'

/**
 * Admin SPA: staff (site owners) only. Customer screens (login, account pages) belong to the public site
 * (`src/public/screens/`).
 */

/**
 * Public pages are a separate, server-rendered app (src/public/): links into them are full page loads.
 * Landing here on the first navigation means the server sent the SPA for this URL (no such page) → 404.
 */
const toPublicSite: NavigationGuard = (to, from) => {
	if (from === START_LOCATION) return { name: 'not-found', params: { pathMatch: to.path.slice(1).split('/') } }
	window.location.assign(to.fullPath)
	return false
}

const staffOnly = { requiresAuth: true, requiresStaff: true }

const router = createRouter({
	history: createWebHistory(),
	routes: [
		{
			path: '/admin/login',
			name: 'admin-login',
			component: () => import('../views/admin/AdminLogin.vue'),
			meta: { guest: true },
		},
		{
			// public home = the page with slug `home` (seeded on a fresh install, DefaultPages.cs)
			path: '/',
			name: 'home',
			component: () => import('../views/NotFound.vue'),
			beforeEnter: toPublicSite,
		},
		{ path: '/home', redirect: '/' },
		{
			path: '/admin',
			name: 'dashboard',
			component: () => import('../views/Dashboard.vue'),
			meta: staffOnly,
		},
		{
			path: '/admin/profile',
			name: 'profile',
			component: () => import('../views/Profile.vue'),
			meta: staffOnly,
		},
		{
			path: '/admin/change-password',
			name: 'change-password',
			component: () => import('../views/ChangePassword.vue'),
			meta: staffOnly,
		},
		// Block editor
		{
			path: '/admin/pages',
			name: 'admin-pages',
			component: () => import('../views/admin/Pages.vue'),
			meta: staffOnly,
		},
		{
			path: '/admin/pages/:id',
			name: 'admin-page-editor',
			component: () => import('../views/admin/PageEditor.vue'),
			props: true,
			meta: { ...staffOnly, hideNav: true },
		},
		{
			path: '/admin/pages/:id/preview',
			name: 'admin-page-preview',
			component: () => import('../views/admin/PagePreview.vue'),
			props: true,
			meta: { ...staffOnly, hideNav: true },
		},
		{
			path: '/admin/media',
			name: 'admin-media',
			component: () => import('../views/admin/Media.vue'),
			meta: staffOnly,
		},
		{
			path: '/admin/menus',
			name: 'admin-menus',
			component: () => import('../views/admin/Menus.vue'),
			meta: staffOnly,
		},
		{
			path: '/admin/configuration',
			name: 'admin-configuration',
			component: () => import('../views/admin/Configuration.vue'),
			meta: staffOnly,
		},
		{
			// Published CMS pages. Static routes above always win (vue-router ranks them higher);
			// the API also rejects slugs that collide with them (PageService.ReservedSlugs).
			path: '/:slug',
			name: 'public-page',
			component: () => import('../views/NotFound.vue'),
			beforeEnter: toPublicSite,
		},
		{
			path: '/:pathMatch(.*)*',
			name: 'not-found',
			component: () => import('../views/NotFound.vue'),
		},
	],
})

router.beforeEach(async (to) => {
	const authStore = useAuthStore()

	if (to.meta.requiresAuth && !(await authStore.ensureSession())) {
		authStore.setRedirectAfterLogin(to.fullPath)
		return { name: 'admin-login' }
	}

	// a customer session: nothing for them here. Real page load: a redirect to `home` on the first navigation
	// would 404 (toPublicSite)
	if (to.meta.requiresStaff && authStore.authType !== 'staff') {
		window.location.replace('/')
		return false
	}

	if (to.meta.guest && authStore.authType === 'staff' && authStore.isAuthenticated) {
		return { name: 'dashboard' }
	}

	return true
})

export default router
