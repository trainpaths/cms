import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { parseConfig } from './site-lang.js'

/**
 * Site palette of the instance, from its `cms.config.json` (`theme.colors`: key → CSS colour). Applied at build time by
 * `cms()`: `themeStyle()` goes into index.html + public.html (`.site-theme { --color-<key>: <value> }`), the colours
 * reach the editor's swatches via `define` (`__CMS_THEME_COLORS__`, src/lib/web-editor/core/colors.ts). Blocks store
 * the key and render `var(--color-<key>)`. The API validates the same key on startup (`CmsConfigLoader`); keep KEY and
 * COLOR in sync with its NamePattern / ColorPattern.
 */

const KEY = /^[a-z0-9]+(?:-[a-z0-9]+)*$/
// hex or a plain colour function: the value lands in a <style> tag, so no ; } < etc.
const COLOR = /^(?:#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})|[a-z]+\([0-9a-zA-Z.,%/ +-]*\))$/

/**
 * @param {string} root app root (where cms.config.json lives)
 * @returns {Record<string, string>}
 */
export function readThemeColors(root) {
	const path = join(root, 'cms.config.json')
	if (!existsSync(path)) return {}
	return themeColors(readFileSync(path, 'utf8'), path)
}

/**
 * @param {string} json cms.config.json content
 * @param {string} [source] for error messages
 * @returns {Record<string, string>}
 */
export function themeColors(json, source = 'cms.config.json') {
	const colors = parseConfig(json, source)?.theme?.colors ?? {}
	if (typeof colors !== 'object' || colors === null || Array.isArray(colors))
		throw new Error(`${source}: theme.colors must be an object (key → colour).`)
	for (const [key, value] of Object.entries(colors)) {
		if (!KEY.test(key)) throw new Error(`${source}: theme.colors key '${key}' must be kebab-case.`)
		if (typeof value !== 'string' || !COLOR.test(value))
			throw new Error(`${source}: theme.colors.${key} '${value}' is not a colour (hex or a function like oklch(...)).`)
	}
	return /** @type {Record<string, string>} */ (colors)
}

/**
 * `<style>` with the palette as CSS variables of the site theme ('' without colours). `:root .site-theme` outranks the
 * instance stylesheet's `.site-theme` rules: the config is the source of these tokens.
 * @param {Record<string, string>} colors validated
 */
export function themeStyle(colors) {
	const vars = Object.entries(colors).map(([key, value]) => `--color-${key}:${value}`)
	return vars.length ? `<style data-cms-theme>:root .site-theme{${vars.join(';')}}</style>` : ''
}

/**
 * Appends `style` to `<head>`.
 * @param {string} html
 * @param {string} style
 */
export const injectThemeStyle = (html, style) => (style ? html.replace(/<\/head>/i, `${style}</head>`) : html)
