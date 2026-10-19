/**
 * Customer screens of the public site (auth + account pages): rendered by the public app like pages, stored by the
 * API next to the not-found page, only with public auth on. Entries = the screen's `PublicState.slug` (path without
 * the leading slash). Mirrors `PublicScreens.All` in `api-backend/Services/Rendering/PublicScreens.cs`. Plain JS:
 * dev-ssr.js reads it too.
 */
export const SCREEN_SLUGS = [
	'login',
	'register',
	'forgot-password',
	'reset-password',
	'verify-email',
	'user/profile',
	'user/password',
]
