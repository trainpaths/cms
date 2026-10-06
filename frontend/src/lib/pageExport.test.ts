import { describe, expect, it } from 'vitest'
import { pageExportFileName, parsePageExport, toPageExport, type PageExportContent } from './pageExport'

const page: PageExportContent = {
	title: 'About',
	slug: 'about',
	metaTitle: 'About us',
	metaDescription: 'Who we are',
	tags: ['team'],
	blocks: [{ id: 'p1', name: 'paragraph', attributes: { text: 'Hi' }, innerBlocks: [] }],
}

describe('page export', () => {
	it('round-trips through JSON into an import request', () => {
		const file = toPageExport(page, new Date('2026-10-06T12:00:00Z'))
		expect(file).toMatchObject({ format: 'cms-page', version: 1, exportedAt: '2026-10-06T12:00:00.000Z' })

		expect(parsePageExport(JSON.stringify(file))).toEqual({
			title: 'About',
			slug: 'about',
			metaTitle: 'About us',
			metaDescription: 'Who we are',
			tags: ['team'],
			blocks: page.blocks,
		})
	})

	it('tolerates missing optional fields', () => {
		const text = JSON.stringify({ format: 'cms-page', version: 1, page: { title: 'X', blocks: [] } })
		expect(parsePageExport(text)).toEqual({
			title: 'X',
			slug: null,
			metaTitle: null,
			metaDescription: null,
			tags: [],
			blocks: [],
		})
	})

	it.each([
		['not json', 'not valid JSON'],
		['[]', 'not a page export'],
		[JSON.stringify({ format: 'other', version: 1 }), 'not a page export'],
		[JSON.stringify({ format: 'cms-page', version: 2, page: {} }), 'newer CMS version'],
		[JSON.stringify({ format: 'cms-page', version: 1, page: { title: ' ', blocks: [] } }), 'incomplete'],
		[JSON.stringify({ format: 'cms-page', version: 1, page: { title: 'X' } }), 'incomplete'],
	])('rejects %s', (text, message) => {
		expect(() => parsePageExport(text)).toThrow(message)
	})

	it('names the file after the slug', () => {
		expect(pageExportFileName('about')).toBe('about.json')
		expect(pageExportFileName('')).toBe('page.json')
	})
})
