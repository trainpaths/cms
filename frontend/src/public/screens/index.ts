import type { PublicState } from '../state'

/** A customer screen of the public site: auth flow (guest) or account page under `/user/*`. */
export interface Screen {
	/** Path without the leading slash = `PublicState.slug` of its stored HTML. */
	slug: string
	/** Route name. */
	name: string
	/** Document title, before the firm name. */
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
	{ slug: 'login', name: 'login', title: 'Log in', account: false },
	{ slug: 'register', name: 'register', title: 'Register', account: false },
	{ slug: 'forgot-password', name: 'forgot-password', title: 'Reset your password', account: false },
	{ slug: 'reset-password', name: 'reset-password', title: 'Choose a new password', account: false },
	{ slug: 'verify-email', name: 'verify-email', title: 'Email verification', account: false },
	{ slug: 'user/profile', name: 'user-profile', title: 'Profile', account: true },
	{ slug: 'user/password', name: 'user-password', title: 'Change password', account: true },
]

/**
 * The screen a state renders (none for pages). The API only renders and serves screens with public auth on: off, a
 * screen path gets the not-found page (state slug `not-found`).
 */
export function screenOf(state: Pick<PublicState, 'slug' | 'page'>): Screen | undefined {
	if (state.page) return undefined
	return SCREENS.find((screen) => screen.slug === state.slug)
}
