import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { localizeBlocks, localizeText } from './localize'
import { missingCounts, pruneTranslations, textFields } from './translatable'
import { registerAllBlocks } from './blockTypes'
import type { BlockInstance } from './types'
import { useEditorStore } from '../../../stores/editor'
import { putApiPagesById } from '../../../api/sdk.gen'
import { useBlockAttribute, useBlockText } from '../composables/useBlockAttribute'
import { usePageText } from '../composables/usePageText'

vi.mock('../../../api/sdk.gen', () => ({ getApiPagesById: vi.fn(), putApiPagesById: vi.fn() }))

registerAllBlocks()

const tree = (): BlockInstance[] => [
	{
		id: 'h',
		name: 'heading',
		attributes: { text: 'Hello', level: 2 },
		translations: { de: { text: 'Hallo', level: 3 }, fr: { text: '' } },
		innerBlocks: [],
	},
	{
		id: 'c',
		name: 'card',
		attributes: { title: 'Card', description: '', mediaId: 'm1' },
		innerBlocks: [{ id: 'l', name: 'link', attributes: { label: 'Go', url: '/about' }, innerBlocks: [] }],
	},
]

describe('localizeBlocks / localizeText (mirror of PageService.Localize)', () => {
	it('overlays non-empty translations, falls back, strips translations', () => {
		const de = localizeBlocks(tree(), 'de', 'en')
		expect(de[0]!.attributes).toEqual({ text: 'Hallo', level: 3 })
		expect(de[0]).not.toHaveProperty('translations')
		expect(localizeBlocks(tree(), 'fr', 'en')[0]!.attributes.text).toBe('Hello')
		expect(localizeBlocks(tree(), 'en', 'en')[0]!.attributes.text).toBe('Hello')
		expect(localizeBlocks(tree(), 'en', 'en')[0]).not.toHaveProperty('translations')
	})

	it('title/meta: translation where non-empty', () => {
		const page = { title: 'About', metaTitle: 'Meta', metaDescription: 'Desc', slug: 'about' }
		const translations = { de: { title: 'Über uns', metaTitle: '', metaDescription: 'Beschreibung' } }
		expect(localizeText(page, translations, 'de', 'en')).toEqual({
			title: 'Über uns',
			metaTitle: 'Meta',
			metaDescription: 'Beschreibung',
			slug: 'about',
		})
		expect(localizeText(page, translations, 'en', 'en').title).toBe('About')
	})
})

describe('textFields', () => {
	const page = { title: 'About', metaTitle: '', metaDescription: 'Desc' }

	it('lists translatable fields with a default value, page fields first', () => {
		expect(textFields(tree(), page, {}).map((f) => `${f.blockId}.${f.key}`)).toEqual([
			'null.title',
			'null.metaDescription',
			'h.text',
			'c.title',
			'l.label',
		])
	})

	it('missing in a language: no translation or an empty one', () => {
		const translations = { de: { title: 'Über uns', metaTitle: '', metaDescription: '' } }
		expect(textFields(tree(), page, translations, 'de').map((f) => `${f.blockId}.${f.key}`)).toEqual([
			'null.metaDescription',
			'c.title',
			'l.label',
		])
		expect(textFields(tree(), page, translations, 'fr').map((f) => `${f.blockId}.${f.key}`)).toContain('h.text')
	})
})

describe('missingCounts / pruneTranslations', () => {
	it('missingCounts matches textFields per language', () => {
		const page = { title: 'About', metaTitle: '', metaDescription: 'Desc' }
		const translations = { de: { title: 'Über uns', metaTitle: '', metaDescription: '' } }
		const counts = missingCounts(tree(), page, translations, ['de', 'fr'])
		expect(counts).toEqual({
			de: textFields(tree(), page, translations, 'de').length,
			fr: textFields(tree(), page, translations, 'fr').length,
		})
	})

	it('drops non-translatable keys, keeps unknown block types as they are', () => {
		const blocks: BlockInstance[] = [
			...tree(),
			{ id: 'x', name: 'gone', attributes: {}, translations: { de: { any: 'thing' } }, innerBlocks: [] },
			{ id: 'i', name: 'image', attributes: { mediaId: 'm' }, translations: { de: { mediaId: 'n' } }, innerBlocks: [] },
		]
		const pruned = pruneTranslations(blocks)
		expect(pruned[0]!.translations).toEqual({ de: { text: 'Hallo' }, fr: { text: '' } })
		expect(pruned[2]!.translations).toEqual({ de: { any: 'thing' } })
		expect(pruned[3]).not.toHaveProperty('translations')
		expect(blocks[0]!.translations!.de).toHaveProperty('level') // input untouched
	})
})

describe('editing a language', () => {
	beforeEach(() => setActivePinia(createPinia()))

	function load() {
		const store = useEditorStore()
		store.pageId = 'p1'
		store.setBlocks(tree())
		return store
	}

	it('useBlockAttribute reads/writes the edited language for translatable attributes only', () => {
		const store = load()
		const heading = () => store.findBlockById('h')!
		const { value: text, original } = useBlockText(heading, 'text')
		const level = useBlockAttribute(heading, 'level', 2)
		expect(text.value).toBe('Hello')
		expect(original.value).toBe('')

		store.editLang = 'fr'
		expect(text.value).toBe('') // empty translation = untranslated
		expect(original.value).toBe('Hello')
		text.value = 'Bonjour'
		expect(heading().translations!.fr).toEqual({ text: 'Bonjour' })
		expect(heading().attributes.text).toBe('Hello')
		level.value = 4 // shared
		expect(heading().attributes.level).toBe(4)

		text.value = '' // removes the key, then the language
		expect(heading().translations!.fr).toBeUndefined()
		store.editLang = 'de'
		expect(text.value).toBe('Hallo')
	})

	it('a translation edit is one undo step and makes the page dirty', () => {
		const store = load()
		store.updateBlockAttributes('h', { level: 2 }) // history baseline
		store.updateBlockTranslation('h', 'it', { text: 'Ciao' })
		expect(store.findBlockById('h')!.translations!.it).toEqual({ text: 'Ciao' })
		store.undo()
		expect(store.findBlockById('h')!.translations!.it).toBeUndefined()
	})

	it('applyTranslations: every field in one undo step, page text in the translations', () => {
		const store = load()
		store.updateBlockAttributes('h', { level: 2 }) // history baseline
		store.applyTranslations('it', [
			{ blockId: null, key: 'title', text: 'Chi siamo' },
			{ blockId: 'c', key: 'title', text: 'Scheda' },
			{ blockId: 'l', key: 'label', text: 'Vai' },
		])
		expect(store.pageTranslations.it!.title).toBe('Chi siamo')
		expect(store.findBlockById('c')!.translations!.it).toEqual({ title: 'Scheda' })
		expect(store.findBlockById('l')!.translations!.it).toEqual({ label: 'Vai' })
		store.undo()
		expect(store.findBlockById('c')!.translations).toBeUndefined()
		expect(store.findBlockById('l')!.translations).toBeUndefined()
		expect(store.pageTranslations.it).toBeUndefined()
		store.redo()
		expect(store.pageTranslations.it!.title).toBe('Chi siamo')
		expect(store.findBlockById('c')!.translations!.it).toEqual({ title: 'Scheda' })
	})

	it('undo of a block change keeps page text typed since', () => {
		const store = load()
		store.updateBlockAttributes('h', { level: 2 })
		store.updateBlockAttributes('h', { level: 3 })
		store.setPageText('de', { title: 'Über uns' })
		store.undo()
		expect(store.pageTranslations.de!.title).toBe('Über uns')
	})

	it('missingTranslations: counts per non-default language', () => {
		const store = load()
		store.pageTitle = 'About'
		const fields = (code: string) =>
			textFields(store.blocks, { title: 'About', metaTitle: '', metaDescription: '' }, {}, code).length
		expect(store.missingTranslations).toEqual({ de: fields('de'), fr: fields('fr'), it: fields('it') })
		expect(store.missingTranslations.de).toBe(fields('fr') - 1) // heading translated in German
	})

	it('a save drops translations of attributes that are not translatable', async () => {
		const store = load()
		store.pageTitle = 'About'
		vi.mocked(putApiPagesById).mockRejectedValueOnce(new Error('stop'))
		store.updateBlockAttributes('h', { level: 2 })
		await store.savePage()
		const sent = vi.mocked(putApiPagesById).mock.calls[0]![0]!.body!.blocks!
		expect(sent[0]!.translations).toEqual({ de: { text: 'Hallo' }, fr: { text: '' } })
	})

	it('page title/meta per language', () => {
		const store = load()
		store.pageTitle = 'About'
		const title = usePageText('title')
		store.editLang = 'de'
		expect(title.value.value).toBe('')
		expect(title.original.value).toBe('About')
		title.value.value = 'Über uns'
		expect(store.pageTranslations.de).toEqual({ title: 'Über uns', metaTitle: '', metaDescription: '' })
		expect(store.pageTitle).toBe('About')
		title.value.value = ''
		expect(store.pageTranslations).toEqual({})
	})
})
