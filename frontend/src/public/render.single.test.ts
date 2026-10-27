import { describe, expect, it } from 'vitest'
import { render } from './entry-server'
import { IS_LOCALIZED } from '../i18n/languages'
import type { PublicState } from './state'

const state = (overrides: Partial<PublicState> = {}): PublicState => ({
	slug: 'about',
	baseUrl: 'https://site.test',
	lang: 'en',
	page: {
		title: 'About',
		slug: 'about',
		publishedAt: null,
		template: null,
		metaTitle: '',
		metaDescription: '',
		media: [],
		blocks: [],
	},
	menu: { handle: 'main', items: [{ label: 'Contact', slug: 'contact', url: null, children: [] }] },
	config: null,
	...overrides,
})

describe('single-language site', () => {
	it('renders unprefixed links, no alternates, no language switch', async () => {
		expect(IS_LOCALIZED).toBe(false)
		const { head, html, tag } = await render(state())
		expect(tag).toBe('en')
		expect(head).toContain('<link rel="canonical" href="https://site.test/about">')
		expect(head).not.toContain('hreflang')
		expect(head).not.toContain('og:locale')
		expect(html).toContain('href="/contact"')
		expect(html).not.toContain('language-dropdown')
		// footer stays hidden without content (no language switch to show)
		expect(html).not.toContain('site-footer')
	})
})
