import { createRouter, createWebHistory, START_LOCATION, type NavigationGuard } from 'vue-router'
import { useAuthStore, type AuthType } from '../stores/auth'
import { useInstanceStore } from '../stores/instance'

/**
 * Public pages are a separate, server-rendered app (src/public/): links into them are full page loads.
 * Landing here on the first navigation means the server sent the SPA for this URL (no such page) → 404.
 */
const toPublicSite: NavigationGuard = (to, from) => {
	if (from === START_LOCATION) return { name: 'not-found', params: { pathMatch: to.path.slice(1).split('/') } }
	window.location.assign(to.fullPath)
	return false
}

const router = createRouter({
	history: createWebHistory(),
	routes: [
		{
			path: '/login',
			name: 'login',
			component: () => import('../views/Login.vue'),
			meta: { guest: true, publicNav: true, publicAuth: true },
		},
		{
			path: '/admin/login',
			name: 'admin-login',
			component: () => import('../views/admin/AdminLogin.vue'),
			meta: { guest: true },
		},
		{
			path: '/register',
			name: 'register',
			component: () => import('../views/Register.vue'),
			meta: { guest: true, publicNav: true, publicAuth: true },
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
			meta: { requiresAuth: true },
		},
		{
			path: '/profile',
			name: 'profile',
			component: () => import('../views/Profile.vue'),
			meta: { requiresAuth: true },
		},
		{
			path: '/change-password',
			name: 'change-password',
			component: () => import('../views/ChangePassword.vue'),
			meta: { requiresAuth: true },
		},
		{
			path: '/forgot-password',
			name: 'forgot-password',
			component: () => import('../views/ForgotPassword.vue'),
			meta: { guest: true, publicNav: true, publicAuth: true },
		},
		{
			// No auth meta: reachable whether or not the user is logged in (clicked from an email).
			path: '/reset-password',
			name: 'reset-password',
			component: () => import('../views/ResetPassword.vue'),
			meta: { publicAuth: true },
		},
		{
			path: '/verify-email',
			name: 'verify-email',
			component: () => import('../views/VerifyEmail.vue'),
			meta: { publicAuth: true },
		},
		// Block editor (staff = site owners)
		{
			path: '/admin/pages',
			name: 'admin-pages',
			component: () => import('../views/admin/Pages.vue'),
			meta: { requiresAuth: true, requiresStaff: true },
		},
		{
			path: '/admin/pages/:id',
			name: 'admin-page-editor',
			component: () => import('../views/admin/PageEditor.vue'),
			props: true,
			meta: { requiresAuth: true, requiresStaff: true, hideNav: true },
		},
		{
			path: '/admin/pages/:id/preview',
			name: 'admin-page-preview',
			component: () => import('../views/admin/PagePreview.vue'),
			props: true,
			meta: { requiresAuth: true, requiresStaff: true, hideNav: true },
		},
		{
			path: '/admin/media',
			name: 'admin-media',
			component: () => import('../views/admin/Media.vue'),
			meta: { requiresAuth: true, requiresStaff: true },
		},
		{
			path: '/admin/menus',
			name: 'admin-menus',
			component: () => import('../views/admin/Menus.vue'),
			meta: { requiresAuth: true, requiresStaff: true },
		},
		{
			path: '/admin/configuration',
			name: 'admin-configuration',
			component: () => import('../views/admin/Configuration.vue'),
			meta: { requiresAuth: true, requiresStaff: true },
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

/** Login screen for a session type: staff sign in at /admin/login, customers at /login. */
export function loginRouteFor(type: AuthType | null) {
	return { name: type === 'staff' ? 'admin-login' : 'login' }
}

router.beforeEach(async (to) => {
	// customer account screens exist only with public auth (instance config)
	if (to.meta.publicAuth && !useInstanceStore().config.publicAuth) {
		return { name: 'not-found', params: { pathMatch: to.path.slice(1).split('/') } }
	}

	const authStore = useAuthStore()

	if (to.meta.requiresAuth && !(await authStore.ensureSession())) {
		authStore.setRedirectAfterLogin(to.fullPath)
		return { name: to.path.startsWith('/admin') ? 'admin-login' : 'login' }
	}

	if (to.meta.requiresStaff && authStore.authType !== 'staff') {
		return { name: 'dashboard' }
	}

	if (to.meta.guest && authStore.isAuthenticated) {
		return { name: 'dashboard' }
	}

	return true
})

export default router
