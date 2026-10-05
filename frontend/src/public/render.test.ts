import { describe, expect, it } from 'vitest'
import { render } from './entry-server'
import { description, documentTitle } from './head'
import type { PublicState } from './state'
import { splice } from '../../server/template'
import { setHtmlLang, siteLang } from '../../server/site-lang'
import type { BlockInstance } from '../lib/web-editor'

const block = (
	name: string,
	attributes: Record<string, unknown>,
	innerBlocks: BlockInstance[] = [],
): BlockInstance => ({
	id: `${name}-${Math.random()}`,
	name,
	attributes,
	innerBlocks,
})

const state = (overrides: Partial<PublicState> = {}): PublicState => ({
	slug: 'about',
	baseUrl: 'https://site.test',
	page: {
		title: 'About <us> $&',
		slug: 'about',
		publishedAt: null,
		template: null,
		metaTitle: '',
		metaDescription: '',
		media: [{ id: 'm1', url: '/api/public/media/m1.png', alt: 'Team' }],
		blocks: [
			block('heading', { text: 'Hello', level: 2 }),
			block('paragraph', { text: 'We build things. </script><script>alert(1)</script>' }),
			block('card', { title: 'Card', mediaId: 'm1' }, [block('link', { label: 'Go', url: 'https://x.test' })]),
		],
	},
	menu: { handle: 'main', items: [{ label: 'Contact', slug: 'contact', url: null, children: [] }] },
	config: {
		fields: { firmName: 'ACME' },
		groups: {
			contact: [
				{
					id: 'a',
					key: 'address',
					label: 'Address',
					type: 'address',
					value: '',
					address: { street: 'Main St 1', postalCode: '12345', city: 'Town', country: '' },
				},
				{ id: 'p', key: 'phone', label: 'Phone', type: 'phone', value: '+49 1', address: null },
				{ id: 'e', key: 'email', label: 'Email', type: 'email', value: '', address: null },
			],
		},
		logo: null,
		icon: null,
		shareImage: null,
		updatedAt: null,
		footerLinks: [{ title: 'Legal', slug: 'legal' }],
	},
	...overrides,
})

describe('render', () => {
	it('renders blocks, nav and footer to HTML', async () => {
		const { html } = await render(state())
		expect(html).toContain('Hello')
		expect(html).toContain('We build things.')
		expect(html).toContain('href="/contact"')
		expect(html).toContain('href="/legal"')
		// footer: firm name + filled contact entries (empty email left out)
		expect(html).toContain('ACME')
		expect(html).toContain('12345 Town')
		expect(html).toContain('href="tel:+491"')
		expect(html).not.toContain('Email')
		expect(html).toContain('src="/api/public/media/m1.png"')
		// staff admin bar is client-only
		expect(html).not.toContain('admin-bar')
	})

	it('builds head tags with escaped values and absolute URLs', async () => {
		const page = { ...state().page!, metaTitle: 'About <us> $&' }
		const { head } = await render(state({ page }))
		expect(head).toContain('<title>About &lt;us&gt; $&amp; - ACME</title>')
		expect(head).toContain('<meta property="og:title" content="About &lt;us&gt; $&amp;">')
		expect(head).toContain('<meta property="og:site_name" content="ACME">')
		expect(head).toContain('<link rel="canonical" href="https://site.test/about">')
		expect(head).toContain('<meta property="og:image" content="https://site.test/api/public/media/m1.png">')
		expect(head).not.toContain('<script>')
	})

	it('prefers the meta description, falls back to the share image', async () => {
		const base = state()
		const page = { ...base.page!, metaDescription: 'Who we are.', blocks: [block('paragraph', { text: 'First.' })] }
		const config = { ...base.config!, shareImage: { id: 's', url: '/api/public/media/s.png', alt: '' } }
		const { head } = await render(state({ page, config }))
		expect(head).toContain('<meta name="description" content="Who we are.">')
		expect(head).toContain('<meta property="og:image" content="https://site.test/api/public/media/s.png">')
	})

	it('embeds state that cannot close its script tag', async () => {
		const { state: json } = await render(state())
		expect(json).not.toContain('</script>')
		expect(JSON.parse(json).page.blocks[1].attributes.text).toContain('</script>')
	})

	it('renders the not-found page as noindex', async () => {
		const { html, head } = await render(state({ slug: 'nope', page: null }))
		expect(html).toContain('Page not found')
		expect(head).toContain('noindex')
	})
})

describe('documentTitle', () => {
	const config = (firmName: string) => ({ ...state().config!, fields: { firmName } })
	const page = (title: string, metaTitle: string) => ({ title, metaTitle })

	it('is "meta title - firm name", or the firm name alone without a meta title', () => {
		expect(documentTitle(page('About', 'Our team'), config('ACME'))).toBe('Our team - ACME')
		expect(documentTitle(page('About', '  '), config('ACME'))).toBe('ACME')
	})

	it('without a firm name: the meta title, then the page title', () => {
		expect(documentTitle(page('About', 'Our team'), config(''))).toBe('Our team')
		expect(documentTitle(page('About', ''), null)).toBe('About')
	})

	it('names the missing page', () => {
		expect(documentTitle(null, config('ACME'))).toBe('Page not found - ACME')
		expect(documentTitle(null, null)).toBe('Page not found')
	})
})

describe('description', () => {
	it('uses the first non-empty paragraph, cut at a word boundary', () => {
		const long = 'word '.repeat(60)
		const text = description([block('paragraph', { text: '  ' }), block('paragraph', { text: long })])
		expect(text.length).toBeLessThanOrEqual(160)
		expect(text.endsWith('word…')).toBe(true)
	})
})

describe('splice', () => {
	it('keeps $ patterns in content literally', () => {
		const out = splice('<!--app-head--><!--app-html--><!--app-state-->', { head: '$&', html: "$'", state: '$1' })
		expect(out).toBe("$&$'$1")
	})
})

describe('site lang (build time)', () => {
	it('reads site.lang, defaulting to en; comments and trailing commas are fine, strings stay untouched', () => {
		expect(siteLang('{}')).toBe('en')
		const json = `{
			// instance config
			"site": { "lang": "de-AT", },
			/* pages */ "pages": [{ "title": "a, ] // b", },],
		}`
		expect(siteLang(json)).toBe('de-AT')
	})

	it('rejects a value that is not a language tag', () => {
		expect(() => siteLang('{ "site": { "lang": "German" } }')).toThrow(/site.lang 'German'/)
	})

	it('replaces the template lang in any quoting, keeps other attributes', () => {
		expect(setHtmlLang('<html lang="en" class="x"><body></body></html>', 'de')).toBe(
			'<html class="x" lang="de"><body></body></html>',
		)
		expect(setHtmlLang("<html lang='en'>", 'de')).toBe('<html lang="de">')
		expect(setHtmlLang('<html lang=en>', 'de')).toBe('<html lang="de">')
		expect(setHtmlLang('<html>', 'de')).toBe('<html lang="de">')
	})
})
