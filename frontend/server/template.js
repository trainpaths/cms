/**
 * Fills the `public.html` placeholders. Function replacers: rendered content may contain `$&`, `$1`...
 * (`<html lang>` is already in the template: `cms()` sets it at build time, see site-lang.js.)
 * @param {string} template
 * @param {import('./template.js').Rendered} rendered
 */
export const splice = (template, { head, html, state }) =>
	template
		.replace('<!--app-head-->', () => head)
		.replace('<!--app-html-->', () => html)
		.replace('<!--app-state-->', () => state)
