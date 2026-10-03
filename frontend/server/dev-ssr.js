import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import { splice } from './template.js'

/**
 * Dev only: server-renders public pages like production (hydration mismatches show up in dev too).
 * Production renders on content change via the `renderer` service instead. A path that isn't a published
 * page (app routes, unknown slugs) falls through to the admin SPA.
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
				const path = (req.url ?? '').split('?')[0]
				if (req.method !== 'GET' || !(path === '/' || /^\/[a-z0-9-]+$/.test(path))) return next()
				const slug = path === '/' ? 'home' : path.slice(1)
				/** @param {string} url */
				const get = (url) =>
					fetch(apiTarget() + url).then(
						(r) => (r.ok ? r.json() : null),
						() => null,
					)
				try {
					const page = await get(`/api/public/pages/${slug}`)
					if (!page) return next()
					const [menu, config] = await Promise.all([
						get('/api/public/menus/main'),
						get('/api/public/site-config'),
					])
					const html = await readFile(resolve(server.config.root, 'public.html'), 'utf8')
					const template = await server.transformIndexHtml(req.url ?? '/', html)
					const { render } = /** @type {{ render: (state: unknown) => Promise<import('./template.js').Rendered> }} */ (
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
