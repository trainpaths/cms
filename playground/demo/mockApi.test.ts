import { beforeEach, describe, expect, it } from 'vitest'
import type { Block, PageDetail, PageSummary, PublicStateResponse } from '@trainpaths/cms/src/api/types.gen.ts'
import { createMockFetch, NOT_IN_DEMO } from './mockApi'
import { DEMO_PAGE_ID } from './seed'
import { createDemoStore, type DemoStore } from './store'

class MemoryStorage implements Storage {
	private items = new Map<string, string>()
	get length() {
		return this.items.size
	}
	clear() {
		this.items.clear()
	}
	getItem(key: string) {
		return this.items.get(key) ?? null
	}
	key(index: number) {
		return [...this.items.keys()][index] ?? null
	}
	removeItem(key: string) {
		this.items.delete(key)
	}
	setItem(key: string, value: string) {
		this.items.set(key, value)
	}
}

const ORIGIN = 'https://demo.test'
let storage: MemoryStorage
let store: DemoStore
let api: typeof fetch

beforeEach(() => {
	storage = new MemoryStorage()
	store = createDemoStore(storage)
	api = createMockFetch(store, { languages: ['en', 'de', 'fr', 'it'] })
})

async function call<T>(method: string, path: string, body?: unknown): Promise<{ status: number; data: T }> {
	const response = await api(
		new Request(ORIGIN + path, {
			method,
			body: body === undefined ? undefined : JSON.stringify(body),
			headers: { 'Content-Type': 'application/json' },
		}),
	)
	return { status: response.status, data: (await response.json()) as T }
}

const texts = (blocks: Block[]): string[] =>
	blocks.flatMap((b) => [String(b.attributes.text ?? ''), ...texts(b.innerBlocks)]).filter(Boolean)

describe('demo mock API', () => {
	it('serves the seeded page, published', async () => {
		const { data: page } = await call<PageDetail>('GET', `/api/pages/${DEMO_PAGE_ID}`)
		expect(page.status).toBe('published')
		expect(page.hasUnpublishedChanges).toBe(false)
		const { data: state } = await call<PublicStateResponse>('GET', '/api/public/state/home')
		expect(state.page?.title).toBe('Welcome')
		expect(state.menu?.items.map((i) => i.label)).toContain('Home')
		expect(state.config.fields.firmName).toBe('Lakeside Studio')
	})

	it('a save changes the working copy, not the view; publishing does', async () => {
		const { data: page } = await call<PageDetail>('GET', `/api/pages/${DEMO_PAGE_ID}`)
		const blocks = [...page.blocks, { id: 'new', name: 'paragraph', attributes: { text: 'Hello demo' }, innerBlocks: [] }]

		const saved = await call<PageSummary>('PUT', `/api/pages/${DEMO_PAGE_ID}`, { blocks })
		expect(saved.data.hasUnpublishedChanges).toBe(true)
		let state = (await call<PublicStateResponse>('GET', '/api/public/state/home')).data
		expect(texts(state.page!.blocks)).not.toContain('Hello demo')

		const published = await call<PageSummary>('PUT', `/api/pages/${DEMO_PAGE_ID}`, { status: 'published' })
		expect(published.data.hasUnpublishedChanges).toBe(false)
		state = (await call<PublicStateResponse>('GET', '/api/public/state/home')).data
		expect(texts(state.page!.blocks)).toContain('Hello demo')
	})

	it('keeps changes in storage and resets to the seed', async () => {
		await call('PUT', `/api/pages/${DEMO_PAGE_ID}`, { title: 'Changed' })
		expect(createDemoStore(storage).data.page.title).toBe('Changed')
		store.reset()
		expect(store.data.page.title).toBe('Welcome')
		expect(createDemoStore(storage).data.page.title).toBe('Welcome')
	})

	it('unpublishing hides the page and its menu link', async () => {
		await call('PUT', `/api/pages/${DEMO_PAGE_ID}`, { status: 'draft' })
		const { data: state } = await call<PublicStateResponse>('GET', '/api/public/state/home')
		expect(state.page).toBeNull()
		expect(state.menu?.items.some((i) => i.slug)).toBe(false)
		expect((await call('GET', '/api/public/pages/home')).status).toBe(404)
	})

	it('localizes the view, falling back to the default text', async () => {
		const { data: de } = await call<PublicStateResponse>('GET', '/api/public/state/home?lang=de')
		expect(de.page?.title).toBe('Willkommen')
		expect(texts(de.page!.blocks)).toContain('Websites aus Blöcken gebaut')
		expect(de.menu?.items[0]?.label).toBe('Start')
		const { data: fr } = await call<PublicStateResponse>('GET', '/api/public/state/home?lang=fr')
		// not translated to French: the English text
		expect(texts(fr.page!.blocks)).toContain('How it works')
	})

	it('refuses uploads, slug changes and everything outside the demo', async () => {
		const upload = await call<{ detail: string }>('POST', '/api/media', {})
		expect(upload).toEqual({ status: 403, data: expect.objectContaining({ detail: NOT_IN_DEMO }) })
		expect((await call('GET', '/api/pages')).status).toBe(403)
		expect((await call('POST', '/api/translate', {})).status).toBe(403)
		expect((await call('PUT', `/api/pages/${DEMO_PAGE_ID}`, { slug: 'other' })).status).toBe(400)
		expect((await call('GET', '/api/pages/other')).status).toBe(404)
	})
})
