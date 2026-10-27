import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

/**
 * Languages of the instance, from its `cms.config.json` (`site.lang`): a tag (`"de-CH"`, one language) or
 * `{ "default": "en-US", "localization": ["de-CH", "fr-CH"] }`. Read at build time by `cms()`: the default tag is the
 * `<html lang>` of index.html + public.html (`transformIndexHtml`), all tags reach the app as `__CMS_LANGUAGES__`
 * (src/i18n/languages.ts). A language's code is its primary subtag (`de`): URL prefix, cookie value, message file.
 * The API validates the same key on startup (`CmsConfigLoader`); keep LANG_TAG and the rules in sync with it.
 */

const LANG_TAG = /^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/
export const DEFAULT_LANG = 'en'

/** @param {string} tag */
export const codeOf = (tag) => /** @type {string} */ (tag.split('-')[0])

/**
 * @param {string} root app root (where cms.config.json lives)
 * @returns {string[]} tags, default first
 */
export function readSiteLanguages(root) {
	const path = join(root, 'cms.config.json')
	if (!existsSync(path)) return [DEFAULT_LANG]
	return siteLanguages(readFileSync(path, 'utf8'), path)
}

/**
 * @param {string} root app root (where cms.config.json lives)
 * @returns {string} the default language's tag
 */
export const readSiteLang = (root) => /** @type {string} */ (readSiteLanguages(root)[0])

/**
 * @param {string} json cms.config.json content (comments + trailing commas allowed, like the API)
 * @param {string} [source] for error messages
 * @returns {string[]} tags, default first
 */
export function siteLanguages(json, source = 'cms.config.json') {
	const lang = parseConfig(json, source)?.site?.lang ?? DEFAULT_LANG
	/** @type {unknown[]} */
	let tags
	if (typeof lang === 'string') {
		tags = [lang]
	} else if (typeof lang === 'object' && lang !== null && !Array.isArray(lang)) {
		const { default: def = DEFAULT_LANG, localization = [], ...rest } = /** @type {Record<string, unknown>} */ (lang)
		const unknown = Object.keys(rest)
		if (unknown.length) throw new Error(`${source}: site.lang has unknown key '${unknown[0]}' (default, localization).`)
		if (!Array.isArray(localization)) throw new Error(`${source}: site.lang.localization must be a list of language tags.`)
		tags = [def, ...localization]
	} else {
		throw new Error(`${source}: site.lang must be a language tag or { "default", "localization" }.`)
	}
	const codes = new Set()
	for (const tag of tags) {
		if (typeof tag !== 'string' || !LANG_TAG.test(tag))
			throw new Error(`${source}: site.lang '${tag}' is not a language tag (e.g. en, de, en-GB).`)
		if (codes.has(codeOf(tag)))
			throw new Error(`${source}: site.lang '${tag}' has the same language code '${codeOf(tag)}' as another language.`)
		codes.add(codeOf(tag))
	}
	return /** @type {string[]} */ (tags)
}

/**
 * @param {string} json cms.config.json content (comments + trailing commas allowed, like the API)
 * @param {string} [source] for error messages
 * @returns {string} the default language's tag
 */
export const siteLang = (json, source = 'cms.config.json') => /** @type {string} */ (siteLanguages(json, source)[0])

/**
 * @param {string} json cms.config.json content (comments + trailing commas allowed, like the API)
 * @param {string} source for error messages
 * @returns {import('./site-lang.d.ts').CmsConfigJson}
 */
export function parseConfig(json, source) {
	try {
		return JSON.parse(stripJsonc(json))
	} catch (e) {
		throw new Error(`${source}: ${/** @type {Error} */ (e).message}`)
	}
}

export { setHtmlLang } from './template.js'

/**
 * Drops comments and trailing commas outside strings (JSON.parse accepts neither).
 * @param {string} text
 */
function stripJsonc(text) {
	let out = ''
	for (let i = 0; i < text.length; i++) {
		const c = text[i]
		if (c === '"') {
			const start = i
			for (i++; i < text.length && text[i] !== '"'; i++) if (text[i] === '\\') i++
			out += text.slice(start, i + 1)
		} else if (c === '/' && text[i + 1] === '/') {
			while (i < text.length && text[i] !== '\n') i++
			out += '\n'
		} else if (c === '/' && text[i + 1] === '*') {
			i = text.indexOf('*/', i + 2)
			if (i === -1) break
			i++
		} else if (c === '}' || c === ']') {
			// trailing comma: last non-space output char (never inside a string, those end with ")
			out = out.replace(/,(\s*)$/, '$1') + c
		} else {
			out += c
		}
	}
	return out
}
