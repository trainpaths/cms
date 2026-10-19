import { SCREENS } from './screens'

/**
 * The `?redirect` target of login/register, or null when it isn't safe to follow: same-site paths only (no `//host`,
 * no backslashes or control characters, which browsers may turn into one), never a guest screen (loop) or the admin
 * (customers have no access).
 */
export function safeRedirect(value: unknown): string | null {
	if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//')) return null
	if (value.includes('\\') || [...value].some((char) => char < ' ')) return null
	const path = value.replace(/[?#].*$/, '')
	if (path === '/admin' || path.startsWith('/admin/')) return null
	if (SCREENS.some((screen) => !screen.account && `/${screen.slug}` === path)) return null
	return value
}
