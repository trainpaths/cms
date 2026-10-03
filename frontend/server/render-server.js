/**
 * Internal render service (compose service `renderer`, never exposed). The API's render worker posts a
 * PublicState and stores the returned HTML; nothing renders at visitor request time.
 *   POST /render   PublicState JSON → full HTML document
 *   GET  /version  hash of template + SSR bundle; the API re-renders everything when it changes (deploys)
 *   GET  /health
 * Run from the app root after its build (reads `dist/public.html` + `dist-ssr/entry-server.js` relative to the
 * working directory): `node node_modules/@trainpaths/cms/server/render-server.js`. Plain JS: Node won't strip
 * types under node_modules. No dependencies needed, the SSR bundle has everything inlined.
 */
import { createServer } from 'node:http'
import { readFile } from 'node:fs/promises'
import { createHash } from 'node:crypto'
import { resolve } from 'node:path'
import { pathToFileURL } from 'node:url'
import { splice } from './template.js'

const MAX_BODY = 4 * 1024 * 1024 // page writes are capped at 2 MB, plus menu + config
const port = Number(process.env.PORT ?? 8080)

const templatePath = resolve('dist/public.html')
const entryPath = resolve('dist-ssr/entry-server.js')
const template = await readFile(templatePath, 'utf8')
/** @type {{ render: (state: unknown) => Promise<import('./template.js').Rendered> }} */
const { render } = await import(pathToFileURL(entryPath).href)
const version = createHash('sha256')
	.update(template)
	.update(await readFile(entryPath))
	.digest('hex')
	.slice(0, 16)

/** @param {import('node:http').IncomingMessage} req */
async function readBody(req) {
	/** @type {Buffer[]} */
	const chunks = []
	let size = 0
	for await (const chunk of req) {
		size += chunk.length
		if (size > MAX_BODY) throw new Error('body too large')
		chunks.push(chunk)
	}
	return Buffer.concat(chunks).toString('utf8')
}

createServer(async (req, res) => {
	try {
		if (req.method === 'GET' && req.url === '/version') return res.end(version)
		if (req.method === 'GET' && req.url === '/health') return res.end('ok')
		if (req.method !== 'POST' || req.url !== '/render') {
			res.statusCode = 404
			return res.end()
		}
		const html = splice(template, await render(JSON.parse(await readBody(req))))
		res.setHeader('Content-Type', 'text/html; charset=utf-8')
		res.setHeader('X-Renderer-Version', version)
		res.end(html)
	} catch (err) {
		console.error('render failed:', err)
		res.statusCode = 500
		res.end(String(err))
	}
}).listen(port, () => console.log(`renderer ${version} listening on :${port}`))
