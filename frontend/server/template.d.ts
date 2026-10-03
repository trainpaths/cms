/** Parts produced by `src/public/entry-server.ts` `render()`. */
export interface Rendered {
	head: string
	html: string
	state: string
}

/** Fills the `public.html` placeholders. */
export declare const splice: (template: string, rendered: Rendered) => string
