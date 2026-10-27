import { createI18n } from 'vue-i18n'
import { inject, type InjectionKey, type Ref } from 'vue'
import { DEFAULT_LANGUAGE, LANGUAGES, type Language } from './languages'

export * from './languages'

/**
 * UI strings (vue-i18n, Composition API). Messages per language code, English is the source and the fallback:
 * - `locales/public/<code>.json`: what the public site shows (header, footer, customer screens, not-found, shared
 *   `common.*`); bundled eagerly, both apps need it and the server render can't wait for a chunk.
 * - `locales/admin/<code>.json`: the admin SPA + editor; a lazy chunk per language (`loadAdminMessages`).
 * - the instance's `src/locales/<code>.json` (root-relative glob = the app): deep-merged over both, to reword package
 *   strings, add its own (blocks, templates) or a language the package doesn't ship (missing keys → English).
 */

type Messages = { [key: string]: string | Messages }

const publicMessages = import.meta.glob<Messages>('../locales/public/*.json', { eager: true, import: 'default' })
const adminMessages = import.meta.glob<Messages>('../locales/admin/*.json', { import: 'default' })
const instanceMessages = import.meta.glob<Messages>('/src/locales/*.json', { eager: true, import: 'default' })

export const SOURCE_LANG = 'en'

const fileOf = (files: Record<string, unknown>, code: string) =>
	Object.keys(files).find((path) => path.endsWith(`/${code}.json`))

// deep copy: every i18n instance gets its own objects, never the imported JSON modules
function merge(target: Messages, source: Messages | undefined): Messages {
	for (const [key, value] of Object.entries(source ?? {})) {
		const current = target[key]
		target[key] = typeof value === 'object' ? merge(typeof current === 'object' ? current : {}, value) : value
	}
	return target
}

/** Every configured language + the English fallback. */
const codes = () => [...new Set([SOURCE_LANG, ...LANGUAGES.map((l) => l.code)])]

const publicOf = (code: string) => {
	const file = fileOf(publicMessages, code)
	return merge(merge({}, file ? publicMessages[file] : undefined), instanceMessages[fileOf(instanceMessages, code)!])
}

/** One i18n per app (per server render): public messages of every language, current locale = `lang`. */
export function createCmsI18n(lang: Language) {
	return createI18n({
		legacy: false,
		locale: lang.code,
		fallbackLocale: SOURCE_LANG,
		messages: Object.fromEntries(codes().map((code) => [code, publicOf(code)])),
		missingWarn: import.meta.env.DEV,
		fallbackWarn: false,
	})
}

export type CmsI18n = ReturnType<typeof createCmsI18n>

/** Adds the admin strings of `code` (+ English, the fallback) to `i18n`; once per language. */
const loaded = new Set<string>()
export async function loadAdminMessages(i18n: CmsI18n, code: string): Promise<void> {
	await Promise.all(
		[SOURCE_LANG, code]
			.filter((c) => !loaded.has(c))
			.map(async (c) => {
				const file = fileOf(adminMessages, c)
				const admin = file ? await adminMessages[file]!() : {}
				// instance strings win over the package's admin strings too
				const messages = merge(merge(publicOf(c), admin), instanceMessages[fileOf(instanceMessages, c)!])
				i18n.global.setLocaleMessage(c, messages)
				loaded.add(c)
			}),
	)
}

/**
 * How the app switches language (the LanguageDropdown calls it): the admin swaps its strings in place, the public
 * site loads the same page under the language's URL. `current` comes from the app's state, never the cookie, so the
 * server render and the hydration show the same markup.
 */
export interface LanguageSwitch {
	current: Ref<Language>
	languages: readonly Language[]
	change: (code: string) => void | Promise<void>
}

export const LANGUAGE_SWITCH: InjectionKey<LanguageSwitch> = Symbol('language-switch')

export const useLanguageSwitch = () => inject(LANGUAGE_SWITCH, null)

/** Dates and numbers in the current language's conventions (`de-CH`: 27.10.2026, 1’000). */
export function useFormat() {
	const languageSwitch = useLanguageSwitch()
	const tag = () => languageSwitch?.current.value.tag ?? DEFAULT_LANGUAGE.tag
	return {
		date: (value: string | number | Date, options?: Intl.DateTimeFormatOptions) =>
			new Date(value).toLocaleDateString(tag(), options),
		dateTime: (value: string | number | Date, options?: Intl.DateTimeFormatOptions) =>
			new Date(value).toLocaleString(tag(), options),
		time: (value: string | number | Date, options?: Intl.DateTimeFormatOptions) =>
			new Date(value).toLocaleTimeString(tag(), options),
		number: (value: number, options?: Intl.NumberFormatOptions) => value.toLocaleString(tag(), options),
	}
}

/**
 * Translation outside components (Pinia stores: error messages): the admin registers its i18n in `createAdmin()`.
 * Without one (tests, the public app) the key comes back as is.
 */
let storeTranslate: (key: string, named?: Record<string, unknown>) => string = (key) => key
export const setStoreTranslate = (t: typeof storeTranslate) => (storeTranslate = t)
export const translate = (key: string, named?: Record<string, unknown>) => storeTranslate(key, named ?? {})
