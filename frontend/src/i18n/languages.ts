/**
 * The instance's languages (`cms.config.json` `site.lang`, read at build time by `cms()`: `__CMS_LANGUAGES__`).
 * A language's **code** is its primary subtag (`de`): URL prefix, `cms_lang` cookie, message file, stored translations;
 * its **tag** (`de-CH`) goes into `<html lang>`, `hreflang` and `Intl` formatting. No vue-i18n here: plain data the
 * public and admin apps (and the server render) share.
 */

declare const __CMS_LANGUAGES__: string[] | undefined

export interface Language {
	code: string
	tag: string
}

export const codeOf = (tag: string) => tag.split('-')[0]!

const tags = typeof __CMS_LANGUAGES__ === 'undefined' || !__CMS_LANGUAGES__.length ? ['en'] : __CMS_LANGUAGES__

/** Default first. */
export const LANGUAGES: readonly Language[] = tags.map((tag) => ({ code: codeOf(tag), tag }))
export const DEFAULT_LANGUAGE: Language = LANGUAGES[0]!
/** More than one language: language switch, prefixed URLs, translations. */
export const IS_LOCALIZED = LANGUAGES.length > 1

/** The configured language of a code, else the default. */
export const languageOf = (code: unknown): Language => LANGUAGES.find((l) => l.code === code) ?? DEFAULT_LANGUAGE

/** Name of the language in itself ("Deutsch", "Français"), for the language switch. */
export function languageLabel(code: string): string {
	let name = code
	try {
		name = new Intl.DisplayNames([code], { type: 'language' }).of(code) ?? code
	} catch {
		// unknown to this runtime's ICU: the code
	}
	return name.charAt(0).toLocaleUpperCase(code) + name.slice(1)
}

/** Cookie the language switch writes; the admin opens in it, the public site redirects unprefixed URLs by it. */
export const LANG_COOKIE = 'cms_lang'

/** `cms_lang` from a `Cookie` header / `document.cookie`, if it names a configured language. */
export function langFromCookie(cookie: string | null | undefined): Language | undefined {
	const value = cookie
		?.split(';')
		.map((part) => part.trim().split('='))
		.find(([name]) => name === LANG_COOKIE)?.[1]
	return LANGUAGES.find((l) => l.code === value)
}

/** Remembers the visitor's choice for a year (readable by the API and the admin, so not HttpOnly). */
export function rememberLanguage(code: string): void {
	const secure = location.protocol === 'https:' ? '; Secure' : ''
	document.cookie = `${LANG_COOKIE}=${code}; Path=/; Max-Age=31536000; SameSite=Lax${secure}`
}
