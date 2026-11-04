import type {
	Block,
	InstanceConfig,
	PageDetail,
	PageSummary,
	ProblemDetails,
	PublicMenu,
	PublicMenuItem,
	PublicPage,
	PublicStateResponse,
	TagUsage,
	UpdatePageRequest,
} from '@trainpaths/cms/src/api/types.gen.ts'
import { localizeBlocks, localizeText } from '@trainpaths/cms/src/lib/web-editor/core/localize.ts'
import cmsConfig from '../cms.config.json'
import { MEDIA, mediaById, toRef } from './media'
import { MENU, SITE_CONFIG, type DemoMenuItem } from './seed'
import { snapshot, type DemoStore } from './store'

export interface MockOptions {
	/** language codes of the site, default first */
	languages: readonly string[]
	/** called when a write couldn't be persisted (the change still applies in memory) */
	onStorageFull?: () => void
	now?: () => Date
}

type Handler = (request: Request, match: RegExpMatchArray, url: URL) => Response | Promise<Response>

const json = (body: unknown, status = 200) =>
	new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

const problem = (status: number, detail: string) =>
	new Response(JSON.stringify({ status, title: detail, detail } satisfies ProblemDetails), {
		status,
		headers: { 'Content-Type': 'application/problem+json' },
	})

export const NOT_IN_DEMO = 'Not available in the demo'
const SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/

const INSTANCE: InstanceConfig = {
	publicAuth: false,
	excludedBlocks: (cmsConfig as { blocks?: { exclude?: string[] } }).blocks?.exclude ?? [],
	siteConfig: {
		fields: [
			{ key: 'firmName', label: 'Firm name', type: 'text', required: false },
			...cmsConfig.siteConfig.fields.map((f) => ({ key: f.key, label: f.label, type: 'text' as const, required: false })),
		],
		groups: [
			{
				key: 'contact',
				label: 'Contact',
				presets: [
					{ key: 'address', label: 'Address', type: 'address' },
					{ key: 'phone', label: 'Phone', type: 'phone' },
					{ key: 'email', label: 'Email', type: 'email' },
				],
				defaults: ['address', 'phone', 'email'],
				required: [],
				allowCustom: true,
			},
			...cmsConfig.siteConfig.groups.map((g) => ({
				key: g.key,
				label: g.label,
				presets: g.presets.map((p) => ({ key: p.key, label: p.label, type: p.type as 'link' })),
				defaults: [],
				required: [],
				allowCustom: g.allowCustom,
			})),
		],
	},
	machineTranslation: false,
}

/** Same JSON as the editor's content check: what makes a page differ from its live copy. */
const content = (p: Pick<PageDetail, 'title' | 'blocks' | 'metaTitle' | 'metaDescription' | 'translations'>) =>
	JSON.stringify([p.title, p.blocks, p.metaTitle, p.metaDescription, p.translations])

function countBlocks(blocks: Block[]): number {
	return blocks.reduce((n, b) => n + 1 + countBlocks(b.innerBlocks), 0)
}

/**
 * `fetch` for the generated API client: answers the calls the editor and the public view make for the demo's one
 * page from `store` (localStorage), mirroring the API's draft/live rules. Everything else is refused with 403.
 */
export function createMockFetch(store: DemoStore, options: MockOptions): typeof fetch {
	const defaultCode = options.languages[0] ?? 'en'
	const now = () => (options.now?.() ?? new Date()).toISOString()
	const langOf = (url: URL) => {
		const code = url.searchParams.get('lang')
		return code && options.languages.includes(code) ? code : defaultCode
	}

	const persist = () => {
		if (!store.save()) options.onStorageFull?.()
	}

	function summary(): PageSummary {
		const p = store.data.page
		return {
			id: p.id,
			title: p.title,
			slug: p.slug,
			status: p.status,
			tags: [...p.tags],
			blockCount: countBlocks(p.blocks),
			createdAt: p.createdAt,
			updatedAt: p.updatedAt,
			publishedAt: p.publishedAt,
			hasUnpublishedChanges: p.hasUnpublishedChanges,
			locked: p.locked,
		}
	}

	function publicPage(slug: string, code: string): PublicPage | null {
		const live = store.data.live
		if (!live || live.slug !== slug) return null
		const text = localizeText(live, live.translations, code, defaultCode)
		return {
			title: text.title,
			slug: live.slug,
			blocks: localizeBlocks(live.blocks, code, defaultCode),
			media: MEDIA.map(toRef),
			publishedAt: live.publishedAt,
			template: null,
			metaTitle: text.metaTitle,
			metaDescription: text.metaDescription,
		}
	}

	function menuItems(items: DemoMenuItem[], code: string): PublicMenuItem[] {
		const live = store.data.live
		return items.flatMap((item): PublicMenuItem[] => {
			// a page link only while the page is published (the API drops links to unpublished pages)
			if (item.slug && !live) return []
			return [
				{
					label: item.label[code] || item.label[defaultCode] || '',
					slug: item.slug ? live!.slug : null,
					url: item.url ?? null,
					children: menuItems(item.children ?? [], code),
				},
			]
		})
	}

	const menu = (code: string): PublicMenu => ({ handle: 'main', items: menuItems(MENU, code) })

	const state = (slug: string | null, code: string): PublicStateResponse => ({
		page: slug ? publicPage(slug, code) : null,
		menu: menu(code),
		config: structuredClone(SITE_CONFIG),
	})

	const pageOr404 = (id: string | undefined) => (id === store.data.page.id ? null : problem(404, 'Page not found.'))

	async function updatePage(request: Request, id: string | undefined): Promise<Response> {
		const missing = pageOr404(id)
		if (missing) return missing
		let body: UpdatePageRequest
		try {
			body = (await request.json()) as UpdatePageRequest
		} catch {
			return problem(400, 'Invalid request body.')
		}
		const data = store.data
		const page = data.page
		if (body.slug != null && body.slug !== page.slug && page.locked)
			return problem(400, 'The slug of this page is fixed.')
		if (body.slug != null && (!SLUG.test(body.slug) || body.slug.length > 100))
			return problem(400, 'Slug must be kebab-case (a-z, 0-9, single hyphens), 1-100 characters.')
		if (body.title != null && !body.title.trim()) return problem(400, 'Title is required.')

		const before = content(page)
		if (body.title != null) page.title = body.title.trim()
		if (body.blocks != null) page.blocks = body.blocks
		if (body.metaTitle != null) page.metaTitle = body.metaTitle
		if (body.metaDescription != null) page.metaDescription = body.metaDescription
		if (body.translations != null) page.translations = body.translations
		if (body.tags != null) page.tags = body.tags
		if (content(page) !== before) page.updatedAt = now()
		if (body.slug != null) {
			page.slug = body.slug
			if (data.live) data.live.slug = body.slug
		}

		if (body.status === 'published') {
			const at = now()
			data.live = snapshot(page, at)
			page.status = 'published'
			page.publishedAt = at
		} else if (body.status === 'draft') {
			data.live = null
			page.status = 'draft'
		}
		page.hasUnpublishedChanges = !!data.live && content(page) !== content(data.live)
		persist()
		return json(summary())
	}

	const routes: [method: string, path: RegExp, handler: Handler][] = [
		['GET', /^\/api\/public\/instance$/, () => json(INSTANCE)],
		['GET', /^\/api\/public\/state$/, (_r, _m, url) => json(state(null, langOf(url)))],
		['GET', /^\/api\/public\/state\/([^/]+)$/, (_r, m, url) => json(state(decodeURIComponent(m[1]!), langOf(url)))],
		[
			'GET',
			/^\/api\/public\/pages\/([^/]+)$/,
			(_r, m, url) => {
				const page = publicPage(decodeURIComponent(m[1]!), langOf(url))
				return page ? json(page) : problem(404, 'Page not found.')
			},
		],
		[
			'GET',
			/^\/api\/public\/menus\/([^/]+)$/,
			(_r, m, url) => (m[1] === 'main' ? json(menu(langOf(url))) : problem(404, 'Menu not found.')),
		],
		['GET', /^\/api\/public\/site-config$/, () => json(SITE_CONFIG)],
		// media URLs from this build (stored ones may point at an older deploy's assets)
		['GET', /^\/api\/pages\/([^/]+)$/, (_r, m) => pageOr404(m[1]) ?? json({ ...store.data.page, media: MEDIA.map(toRef) })],
		['PUT', /^\/api\/pages\/([^/]+)$/, (r, m) => updatePage(r, m[1])],
		['GET', /^\/api\/media$/, () => json(MEDIA)],
		[
			'GET',
			/^\/api\/media\/([^/]+)$/,
			(_r, m) => {
				const item = mediaById(m[1]!)
				return item ? json(item) : problem(404, 'Media not found.')
			},
		],
		['GET', /^\/api\/tags$/, () => json(store.data.page.tags.map((name): TagUsage => ({ name, pageCount: 1 })))],
	]

	return async (input, init) => {
		const request = input instanceof Request ? input : new Request(input, init)
		const url = new URL(request.url)
		for (const [method, path, handler] of routes) {
			const match = url.pathname.match(path)
			if (match && method === request.method) return handler(request, match, url)
		}
		return problem(403, NOT_IN_DEMO)
	}
}
