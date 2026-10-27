// self-contained: renderer images copy only this file + render-server.js (Dockerfile, instance Dockerfiles)

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
