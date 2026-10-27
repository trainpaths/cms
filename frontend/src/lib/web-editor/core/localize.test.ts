import { describe, expect, it } from 'vitest'
import { localizeBlocks, localizeText } from './localize'
import fixture from './localize.cases.json'
import type { BlockInstance, PageTextTranslation } from './types'

interface LocalizeCase {
	name: string
	code: string
	defaultCode: string
	page: { title: string; metaTitle: string; metaDescription: string; translations: Record<string, PageTextTranslation> }
	blocks: BlockInstance[]
	expected: { title: string; metaTitle: string; metaDescription: string; blocks: BlockInstance[] }
}

// same cases as the API's PageService.Localize test: the admin preview must show what the public site will
describe('localize (shared cases with PageService.Localize)', () => {
	for (const c of fixture.cases as unknown as LocalizeCase[])
		it(c.name, () => {
			const { translations, ...text } = c.page
			const { blocks, ...expectedText } = c.expected
			expect(localizeText(text, translations, c.code, c.defaultCode)).toEqual(expectedText)
			expect(localizeBlocks(c.blocks, c.code, c.defaultCode)).toEqual(blocks)
		})
})
