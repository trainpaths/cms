/** Parts produced by `src/public/entry-server.ts` `render()`. */
export interface Rendered {
	head: string
	html: string
	state: string
	/** Language tag of the render → `<html lang>` (the template has the default's). */
	tag: string
}

/** Fills the `public.html` placeholders + sets `<html lang>` to `rendered.tag`. */
export declare const splice: (template: string, rendered: Rendered) => string

/** Sets `lang` on the `<html>` tag, replacing the template's own. */
export declare const setHtmlLang: (html: string, lang: string) => string
