import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { basename, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { loadEnv } from 'vite'
import { nbUi } from '@trainpaths/nb-ui/vite'
import { devSsr } from './server/dev-ssr.js'
import { readSiteLang, setHtmlLang } from './server/site-lang.js'

const PKG = '@trainpaths/cms'
// real path of the installed package (pnpm symlinks resolve to it, like Vite's importers)
const pkgDir = fileURLToPath(new URL('.', import.meta.url))
const pkgPublic = join(pkgDir, 'public')

/**
 * Vite setup of a CMS instance (an app that imports `@trainpaths/cms`): vue, Tailwind, nb-ui overrides, dev SSR
 * of public pages, plus:
 * - inputs `index.html` (admin SPA) + `public.html` (public site template) from the app root, `/api` proxy to
 *   `VITE_API_BASE_URL` (from the app's envDir) or `apiTarget`
 * - overrides: an import of `*.vue` from inside the package resolves to the app's `src/overrides/<basename>` if
 *   it exists (nb-ui components: `src/overrides/ui/`). Wrap the original via
 *   `import Base from '@trainpaths/cms/src/<path>.vue'`. Adding/removing an override needs a dev server restart.
 * - the package's `public/` files (CMS favicon) are served/emitted unless the app's publicDir has the same file
 * - `<html lang>` of both HTML files = `site.lang` of the app's `cms.config.json` (read per transform: dev picks up edits)
 * Plain JS on purpose: Node won't strip types in node_modules and Vite's config loader externalizes deps.
 *
 * @param {{ apiTarget?: string }} [options]
 * @returns {import('vite').PluginOption[]}
 */
export function cms({ apiTarget } = {}) {
	let target = apiTarget
	let root = ''
	let overrides = ''
	let publicDir = ''
	let ssrBuild = false
	const pkgPublicFiles = existsSync(pkgPublic) ? readdirSync(pkgPublic) : []
	/** @param {string} name */
	const appHasPublic = (name) => !!publicDir && existsSync(join(publicDir, name))

	/** @type {import('vite').Plugin} */
	const core = {
		name: 'cms',
		enforce: 'pre',
		config(config, { command, mode }) {
			const root = resolve(config.root ?? process.cwd())
			target ??= loadEnv(mode, config.envDir ? resolve(root, config.envDir) : root).VITE_API_BASE_URL
			return {
				resolve: { dedupe: ['vue', 'vue-router', 'pinia'] },
				// raw .vue/.ts source: the dep pre-bundler can't compile it, Vite's own pipeline does
				optimizeDeps: { exclude: [PKG] },
				build: {
					rollupOptions: {
						input: { main: resolve(root, 'index.html'), public: resolve(root, 'public.html') },
					},
				},
				// build: one self-contained SSR bundle, renderer needs no node_modules. dev: only raw-source packages;
				// inlining everything breaks CJS deps (vue's index.js) in the dev module runner
				ssr: { noExternal: command === 'build' ? true : [PKG, '@trainpaths/nb-ui'] },
				server: { proxy: { '/api': { target, changeOrigin: true } } },
			}
		},
		configResolved(config) {
			root = config.root
			overrides = resolve(config.root, 'src/overrides')
			publicDir = config.publicDir
			ssrBuild = !!config.build.ssr
		},
		transformIndexHtml: {
			order: 'pre',
			handler: (html) => setHtmlLang(html, readSiteLang(root)),
		},
		resolveId(source, importer) {
			if (!importer?.startsWith(pkgDir) || !source.endsWith('.vue')) return
			const local = join(overrides, basename(source))
			if (existsSync(local)) return local
		},
		configureServer(server) {
			server.middlewares.use((req, res, next) => {
				const name = (req.url ?? '').split('?')[0].slice(1)
				if (!pkgPublicFiles.includes(name) || appHasPublic(name)) return next()
				if (name.endsWith('.svg')) res.setHeader('Content-Type', 'image/svg+xml')
				res.end(readFileSync(join(pkgPublic, name)))
			})
		},
		generateBundle() {
			if (ssrBuild) return
			for (const name of pkgPublicFiles) {
				if (!appHasPublic(name)) {
					this.emitFile({ type: 'asset', fileName: name, source: readFileSync(join(pkgPublic, name)) })
				}
			}
		},
	}

	return [vue(), tailwindcss(), nbUi({ dir: 'src/overrides/ui' }), core, devSsr(() => target)]
}
