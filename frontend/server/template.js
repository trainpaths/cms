import { setHtmlLang } from './site-lang.js'

/**
 * Fills the `public.html` placeholders and sets `<html lang>` to the render's language (the template carries the
 * default's, set by `cms()` at build time). Function replacers: rendered content may contain `$&`, `$1`...
 * @param {string} template
 * @param {import('./template.js').Rendered} rendered
 */
export const splice = (template, { head, html, state, tag }) =>
	setHtmlLang(template, tag)
		.replace('<!--app-head-->', () => head)
		.replace('<!--app-html-->', () => html)
		.replace('<!--app-state-->', () => state)
