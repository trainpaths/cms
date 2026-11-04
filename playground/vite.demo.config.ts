import { resolve } from 'node:path'
import { defineConfig, type Plugin } from 'vite'
import { cms } from '@trainpaths/cms/vite'

/**
 * Browser-only demo (demo/, published to GitHub Pages): the playground's blocks, theme and locales with an
 * in-browser mock of the API. Nothing here changes the normal build (vite.config.ts).
 */
const root = import.meta.dirname
const page = resolve(root, 'demo.html')
const noUpload = resolve(root, 'demo/NoUpload.vue')

const demo: Plugin = {
	name: 'cms-demo',
	// pre: before Vite's own resolver, so the upload button swap below wins
	enforce: 'pre',
	configResolved(config) {
		// cms() sets index.html + public.html as inputs: the demo is its own single page
		config.build.rollupOptions.input = { demo: page }
	},
	resolveId(source, importer) {
		// no image upload: the picker only offers the seeded images
		if (importer && source.endsWith('/MediaUploadButton.vue')) return noUpload
	},
	configureServer(server) {
		server.middlewares.use((req, _res, next) => {
			if (req.url === '/' || req.url?.startsWith('/?')) req.url = '/demo.html'
			next()
		})
	},
	generateBundle: {
		// after Vite's HTML plugin emitted it; GitHub Pages serves index.html at the root
		order: 'post',
		handler(_options, bundle) {
			const html = bundle['demo.html']
			if (html) html.fileName = 'index.html'
		},
	},
}

export default defineConfig({
	// no API and no server rendering: the mock answers every /api call in the browser
	plugins: [cms().filter((plugin) => !(plugin && 'name' in plugin && plugin.name === 'cms-dev-ssr')), demo],
	// relative asset URLs: GitHub Pages serves the repo under /cms/
	base: './',
	build: { outDir: 'dist-demo', emptyOutDir: true },
})
