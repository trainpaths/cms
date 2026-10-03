import type { PluginOption } from 'vite'

export interface CmsOptions {
	/** API origin for the dev `/api` proxy and dev SSR; default `VITE_API_BASE_URL` from the app's envDir. */
	apiTarget?: string
}

/** Vite plugins of a CMS instance; see `vite-plugin.js`. */
export declare function cms(options?: CmsOptions): PluginOption[]
