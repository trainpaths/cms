/** Parts produced by `src/public/entry-server.ts` `render()`. */
export interface Rendered {
	head: string
	html: string
	state: string
	/** Language tag of the render → `<html lang>` (the template has the default's). */
	tag: string
}

/** Fills the `public.html` placeholders. */
export declare const splice: (template: string, rendered: Rendered) => string
