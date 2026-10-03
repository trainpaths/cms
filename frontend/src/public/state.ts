import type { PublicMenu, PublicPage, SiteConfig } from '../lib/web-editor'

/**
 * Everything one public page renders from. The API builds it (render worker → renderer `POST /render`),
 * the server render embeds it as `#__STATE__`, and the client hydrates from it.
 */
export interface PublicState {
	/** Requested slug (`home` for `/`). */
	slug: string
	/** null → not-found page. */
	page: PublicPage | null
	/** The `main` menu. */
	menu: PublicMenu | null
	config: SiteConfig | null
	/** Absolute site origin for canonical/OG URLs, e.g. `https://example.com` (no trailing slash). */
	baseUrl: string
}

/** Public path of a slug: `home` lives at `/`. */
export const pathOf = (slug: string) => (slug === 'home' ? '/' : `/${slug}`)
