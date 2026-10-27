import type { PublicState } from '../state'

/** A customer screen of the public site: auth flow (guest) or account page under `/user/*`. */
export interface Screen {
	/** Path without the leading slash = `PublicState.slug` of its stored HTML. */
	slug: string
	/** Route name. */
	name: string
	/** Message key of the document title (before the firm name). */
	title: string
	/** Account page: needs a customer session (checked after mount, see `session.ts`). */
	account: boolean
}

declare module 'vue-router' {
	interface RouteMeta {
		screen?: Screen
	}
}

/** Same slugs as `server/screens.js` (dev SSR) and the API's `PublicScreens` (render.test.ts checks the first). */
export const SCREENS: readonly Screen[] = [
	{ slug: 'login', name: 'login', title: 'screens.login.title', account: false },
	{ slug: 'register', name: 'register', title: 'screens.register.title', account: false },
	{ slug: 'forgot-password', name: 'forgot-password', title: 'screens.forgotPassword.title', account: false },
	{ slug: 'reset-password', name: 'reset-password', title: 'screens.resetPassword.title', account: false },
	{ slug: 'verify-email', name: 'verify-email', title: 'screens.verifyEmail.title', account: false },
	{ slug: 'user/profile', name: 'user-profile', title: 'screens.profile.title', account: true },
	{ slug: 'user/password', name: 'user-password', title: 'screens.password.title', account: true },
]

/**
 * The screen a state renders (none for pages). The API only renders and serves screens with public auth on: off, a
 * screen path gets the not-found page (state slug `not-found`).
 */
export function screenOf(state: Pick<PublicState, 'slug' | 'page'>): Screen | undefined {
	if (state.page) return undefined
	return SCREENS.find((screen) => screen.slug === state.slug)
}
