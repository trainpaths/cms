import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePagesStore } from './pages'
import type { PageDetail, PageSummary } from '../lib/web-editor/core/types'

vi.mock('../api/sdk.gen', () => ({ getApiPages: vi.fn(), getApiPagesById: vi.fn() }))

const detail = (): PageDetail => ({
	id: 'p1',
	title: 'Old',
	slug: 'old',
	status: 'draft',
	tags: ['a'],
	blocks: [{ id: 'b1', name: 'paragraph', attributes: {}, innerBlocks: [] }],
	media: [],
	createdAt: '2026-01-01T00:00:00Z',
	updatedAt: '2026-01-01T00:00:00Z',
	publishedAt: null,
	hasUnpublishedChanges: false,
	template: null,
	locked: false,
	editable: true,
	metaTitle: '',
	metaDescription: '',
	translations: {},
})

const summary = (patch: Partial<PageSummary> = {}): PageSummary => ({
	id: 'p1',
	title: 'Old',
	slug: 'old',
	status: 'draft',
	tags: ['a'],
	blockCount: 1,
	createdAt: '2026-01-01T00:00:00Z',
	updatedAt: '2026-01-01T00:00:00Z',
	publishedAt: null,
	hasUnpublishedChanges: false,
	locked: false,
	...patch,
})

describe('applySaved (PUT answers with the summary only)', () => {
	beforeEach(() => setActivePinia(createPinia()))

	it('replaces the list row and merges summary + sent fields into the cached detail', () => {
		const store = usePagesStore()
		store.remember(detail())
		store.items = [summary()]
		const blocks = [
			{ id: 'b1', name: 'paragraph', attributes: {}, innerBlocks: [] },
			{ id: 'b2', name: 'paragraph', attributes: {}, innerBlocks: [] },
		]
		const saved = summary({ title: 'New', status: 'published', blockCount: 2, updatedAt: '2026-02-01T00:00:00Z' })

		store.applySaved(saved, { title: 'New', blocks, metaTitle: 'Meta' })

		expect(store.items[0]).toEqual(saved)
		const cached = store.cached('p1')!
		expect(cached).toMatchObject({
			title: 'New',
			status: 'published',
			updatedAt: '2026-02-01T00:00:00Z',
			metaTitle: 'Meta',
		})
		expect(cached.blocks).toEqual(blocks)
		// detail keeps its own fields; no summary-only ones leak in
		expect(cached.editable).toBe(true)
		expect(cached).not.toHaveProperty('blockCount')
	})

	it('without a cached detail only updates the list', () => {
		const store = usePagesStore()
		store.items = [summary()]
		store.applySaved(summary({ tags: ['a', 'b'] }))
		expect(store.items[0]!.tags).toEqual(['a', 'b'])
		expect(store.cached('p1')).toBeNull()
	})

	it('the cached copy is not shared with the caller', () => {
		const store = usePagesStore()
		store.remember(detail())
		const blocks = detail().blocks
		store.applySaved(summary(), { blocks })
		blocks.pop()
		expect(store.cached('p1')!.blocks).toHaveLength(1)
	})
})
