import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import { splice } from './template.js'
import { SCREEN_SLUGS } from './screens.js'
import { codeOf, readSiteLanguages } from './site-lang.js'

/**
 * Dev only: server-renders public pages and customer screens like production (hydration mismatches show up in dev
 * too). Production renders on content change via the `renderer` service instead. A path that is neither a published
 * page nor a screen (admin routes, unknown slugs, screens with public auth off) falls through to the admin SPA.
 * Reads the app's `public.html` and SSR entry (`/src/entry-server.ts`) from the Vite root. Language prefixes and the
 * `cms_lang` redirect like `PublicHtmlController` (API); unknown prefixes fall through.
 *
 * @param {() => string | undefined} apiTarget API origin (resolved once the config is known)
 * @returns {import('vite').Plugin}
 */
export function devSsr(apiTarget) {
	return {
		name: 'cms-dev-ssr',
		apply: 'serve',
		configureServer(server) {
			// once: cms() restarts the dev server when the languages change
			const codes = readSiteLanguages(server.config.root).map(codeOf)
			const [defaultCode] = codes
			server.middlewares.use(async (req, res, next) => {
				const [path = '', query] = (req.url ?? '').split(/\?(.*)/s)
				const match = /^\/(?:([a-z]{2,3})(?=\/|$))?\/?((?:user\/)?[a-z0-9-]*)$/.exec(path)
				if (req.method !== 'GET' || !match) return next()
				// a 2-3 letter page slug (/faq) unless it is a configured code
				const known = !!match[1] && codes.includes(match[1])
				const prefix = known ? match[1] : undefined
				const rest = known ? (match[2] ?? '') : path.slice(1)
				if (!/^(user\/)?[a-z0-9-]*$/.test(rest)) return next()
				const search = query ? `?${query}` : ''
				/** @param {number} status @param {string} location */
				const redirect = (status, location) => {
					res.statusCode = status
					res.setHeader('Location', location + search)
					res.end()
				}
				if (prefix === defaultCode && codes.length > 1) return redirect(301, `/${rest}`)
				const lang = prefix ?? /** @type {string} */ (defaultCode)
				const slug = rest || 'home'
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
						await get(`/api/public/state${screen ? '' : `/${slug}`}?lang=${lang}`)
					)
					const screenOn = screen && !!(/** @type {{ publicAuth?: boolean } | null} */ (await get('/api/public/instance'))?.publicAuth)
					if (!state?.page && !screenOn) return next()
					// only pages + screens (admin routes fell through above)
					const cookie = /(?:^|;\s*)cms_lang=([^;]*)/.exec(req.headers.cookie ?? '')?.[1]
					if (!prefix && codes.length > 1 && cookie && cookie !== defaultCode && codes.includes(cookie))
						return redirect(302, rest ? `/${cookie}/${rest}` : `/${cookie}`)
					const { page = null, menu = null, config = null } = state ?? {}
					const html = await readFile(resolve(server.config.root, 'public.html'), 'utf8')
					const template = await server.transformIndexHtml(req.url ?? '/', html)
					const { render } =
						/** @type {{ render: (state: unknown) => Promise<import('./template.js').Rendered> }} */ (
							await server.ssrLoadModule('/src/entry-server.ts')
						)
					const rendered = await render({ slug, page, menu, config, baseUrl: `http://${req.headers.host}`, lang })
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
