import { DEFAULT_LANGUAGE, LANGUAGES } from '../i18n/languages'

/**
 * Language prefixes of public URLs: the default language is unprefixed (`/about`), every other one prefixed with its
 * code (`/de/about`, `/de` = German home). Mirrors `PublicHtmlController` (API) and `server/dev-ssr.js`.
 */

/** Router base of a language: `/` or `/de/`. */
export const localeBase = (code: string) => (code === DEFAULT_LANGUAGE.code ? '/' : `/${code}/`)

/** `'/about', 'de'` → `/de/about`; `'/', 'de'` → `/de`; the default language → unchanged. */
export function localePath(path: string, code: string): string {
	if (code === DEFAULT_LANGUAGE.code) return path
	return path === '/' ? `/${code}` : `/${code}${path}`
}

/** `/de/about` → `{ code: 'de', path: '/about' }`; unprefixed, unknown or default prefix → the default language. */
export function stripLocale(pathname: string): { code: string; path: string } {
	const [, first, rest] = /^\/([^/]+)(\/.*)?$/.exec(pathname) ?? []
	if (first && first !== DEFAULT_LANGUAGE.code && LANGUAGES.some((l) => l.code === first))
		return { code: first, path: rest && rest !== '/' ? rest : '/' }
	return { code: DEFAULT_LANGUAGE.code, path: pathname }
}
