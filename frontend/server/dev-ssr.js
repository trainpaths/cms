import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import { splice } from './template.js'
import { SCREEN_SLUGS } from './screens.js'

/**
 * Dev only: server-renders public pages and customer screens like production (hydration mismatches show up in dev
 * too). Production renders on content change via the `renderer` service instead. A path that is neither a published
 * page nor a screen (admin routes, unknown slugs, screens with public auth off) falls through to the admin SPA.
 * Reads the app's `public.html` and SSR entry (`/src/entry-server.ts`) from the Vite root.
 *
 * @param {() => string | undefined} apiTarget API origin (resolved once the config is known)
 * @returns {import('vite').Plugin}
 */
export function devSsr(apiTarget) {
	return {
		name: 'cms-dev-ssr',
		apply: 'serve',
		configureServer(server) {
			server.middlewares.use(async (req, res, next) => {
				const path = (req.url ?? '').split('?')[0] ?? ''
				if (req.method !== 'GET' || !(path === '/' || /^\/(user\/)?[a-z0-9-]+$/.test(path))) return next()
				const slug = path === '/' ? 'home' : path.slice(1)
				const screen = SCREEN_SLUGS.includes(slug)
				/** @param {string} url */
				const get = (url) =>
					fetch(`${apiTarget()}${url}`).then(
						(r) => (r.ok ? r.json() : null),
						() => null,
					)
				try {
					// page + main menu + site config in one call (screens: no page, only with public auth)
					const state = /** @type {{ page?: unknown, menu?: unknown, config?: unknown } | null} */ (
						await get(`/api/public/state${screen ? '' : `/${slug}`}`)
					)
					const screenOn = screen && !!(/** @type {{ publicAuth?: boolean } | null} */ (await get('/api/public/instance'))?.publicAuth)
					if (!state?.page && !screenOn) return next()
					const { page = null, menu = null, config = null } = state ?? {}
					const html = await readFile(resolve(server.config.root, 'public.html'), 'utf8')
					const template = await server.transformIndexHtml(req.url ?? '/', html)
					const { render } =
						/** @type {{ render: (state: unknown) => Promise<import('./template.js').Rendered> }} */ (
							await server.ssrLoadModule('/src/entry-server.ts')
						)
					const rendered = await render({ slug, page, menu, config, baseUrl: `http://${req.headers.host}` })
					res.setHeader('Content-Type', 'text/html; charset=utf-8')
					res.end(splice(template, rendered))
				} catch (err) {
					server.ssrFixStacktrace(/** @type {Error} */ (err))
					next(err)
				}
			})
		},
	}
}
