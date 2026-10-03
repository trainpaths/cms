import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

/**
 * `<html lang>` of the instance, from its `cms.config.json` (`site.lang`, default `en`). Set at build time by `cms()`
 * (`transformIndexHtml`: index.html + public.html, dev and build), so pages carry it without runtime work. The API
 * validates the same key on startup (`CmsConfigLoader`); keep LANG_TAG in sync with its LangPattern.
 */

const LANG_TAG = /^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/
export const DEFAULT_LANG = 'en'

/**
 * @param {string} root app root (where cms.config.json lives)
 * @returns {string}
 */
export function readSiteLang(root) {
	const path = join(root, 'cms.config.json')
	if (!existsSync(path)) return DEFAULT_LANG
	return siteLang(readFileSync(path, 'utf8'), path)
}

/**
 * @param {string} json cms.config.json content (comments + trailing commas allowed, like the API)
 * @param {string} [source] for error messages
 * @returns {string}
 */
export function siteLang(json, source = 'cms.config.json') {
	let config
	try {
		config = JSON.parse(stripJsonc(json))
	} catch (e) {
		throw new Error(`${source}: ${/** @type {Error} */ (e).message}`)
	}
	const lang = config?.site?.lang ?? DEFAULT_LANG
	if (typeof lang !== 'string' || !LANG_TAG.test(lang))
		throw new Error(`${source}: site.lang '${lang}' is not a language tag (e.g. en, de, en-GB).`)
	return lang
}

/**
 * Sets `lang` on the `<html>` tag, replacing the template's own (any quoting).
 * @param {string} html
 * @param {string} lang validated tag (letters, digits, hyphens: no escaping needed)
 */
export const setHtmlLang = (html, lang) =>
	html.replace(
		/<html\b([^>]*)>/i,
		(_, /** @type {string} */ attrs) =>
			`<html${attrs.replace(/\slang=("[^"]*"|'[^']*'|[^\s>]*)/i, '')} lang="${lang}">`,
	)

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
