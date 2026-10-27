import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { localizeBlocks, localizeText } from './localize'
import { textFields } from './translatable'
import { registerAllBlocks } from './blockTypes'
import type { BlockInstance } from './types'
import { useEditorStore } from '../../../stores/editor'
import { useAttributeFallback, useBlockAttribute } from '../composables/useBlockAttribute'
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
			'l.url',
		])
	})

	it('missing in a language: no translation or an empty one', () => {
		const translations = { de: { title: 'Über uns', metaTitle: '', metaDescription: '' } }
		expect(textFields(tree(), page, translations, 'de').map((f) => `${f.blockId}.${f.key}`)).toEqual([
			'null.metaDescription',
			'c.title',
			'l.label',
			'l.url',
		])
		expect(textFields(tree(), page, translations, 'fr').map((f) => `${f.blockId}.${f.key}`)).toContain('h.text')
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
		const text = useBlockAttribute(heading, 'text', '')
		const level = useBlockAttribute(heading, 'level', 2)
		const original = useAttributeFallback(heading, 'text')
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
