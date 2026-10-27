import { SCREENS } from './screens'
import { stripLocale } from './locale'

/**
 * The `?redirect` target of login/register, or null when it isn't safe to follow: same-site paths only (no `//host`,
 * no backslashes or control characters, which browsers may turn into one), never a guest screen (loop) or the admin
 * (customers have no access). Router paths, without the language prefix (the router adds it): a prefixed value
 * (`/de/about`, an old link) is stripped.
 */
export function safeRedirect(value: unknown): string | null {
	if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//')) return null
	if (value.includes('\\') || [...value].some((char) => char < ' ')) return null
	const suffix = /[?#].*$/.exec(value)?.[0] ?? ''
	const path = stripLocale(value.slice(0, value.length - suffix.length)).path
	if (path === '/admin' || path.startsWith('/admin/')) return null
	if (SCREENS.some((screen) => !screen.account && `/${screen.slug}` === path)) return null
	return path + suffix
}
