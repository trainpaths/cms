import { describe, expect, it } from 'vitest'
import templateSource from '../../server/template.js?raw'
import { render } from './entry-server'
import { description, documentTitle } from './head'
import type { PublicState } from './state'
import { splice } from '../../server/template'
import { setHtmlLang, siteLang, siteLanguages } from '../../server/site-lang'
import { injectThemeStyle, themeColors, themeStyle } from '../../server/theme'
import { SCREEN_SLUGS } from '../../server/screens'
import { SCREENS } from './screens'
import { createPublicRouter } from './router'
import { usePublicPageStore } from './store'
import { createPinia } from 'pinia'
import { createMemoryHistory } from 'vue-router'
import type { BlockInstance } from '../lib/web-editor'
import { localeBase, localePath, stripLocale } from './locale'
import { safeRedirect } from './redirect'

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
	lang: 'en',
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

	it('renders theme colour keys as site theme variables, legacy hex as is, drops anything else', async () => {
		const page = {
			...state().page!,
			blocks: [
				block('paragraph', { text: 'a', textColor: 'brand-dark', backgroundColor: '#ff0000' }),
				block('paragraph', { text: 'b', textColor: 'red;position:fixed' }),
			],
		}
		const { html } = await render(state({ page }))
		expect(html).toContain('background-color:#ff0000;color:var(--color-brand-dark);')
		expect(html).not.toContain('position:fixed')
	})

	it('renders the not-found page as noindex', async () => {
		const { html, head } = await render(state({ slug: 'nope', page: null }))
		expect(html).toContain('Page not found')
		expect(head).toContain('noindex')
	})
})

describe('customer screens', () => {
	it('render inside the site layout, titled and noindex', async () => {
		const { html, head } = await render(state({ slug: 'login', page: null }))
		expect(html).toContain('<form')
		expect(html).toContain('placeholder="Password"')
		expect(html).toContain('href="/contact"') // header
		expect(html).toContain('ACME') // footer
		expect(head).toContain('<title>Log in - ACME</title>')
		expect(head).toContain('noindex')
	})

	it('render account pages as a shell without user data', async () => {
		const { html, head } = await render(state({ slug: 'user/profile', page: null }))
		expect(html).toContain('Profile')
		expect(html).not.toContain('Logout')
		expect(head).toContain('<title>Profile - ACME</title>')
	})

	it('render links without the redirect query (added after hydration)', async () => {
		const { html } = await render(state({ slug: 'login', page: null }))
		expect(html).toContain('href="/register"')
	})

	it('hydrate as not-found when the server sent the not-found page for a screen path (public auth off)', async () => {
		const pinia = createPinia()
		usePublicPageStore(pinia).set('not-found', null)
		const router = createPublicRouter(createMemoryHistory(), pinia, (key) => key)
		await router.push('/login?x=1')
		expect(router.currentRoute.value.name).toBe('external')
		expect(router.currentRoute.value.fullPath).toBe('/login?x=1')
	})

	it('hydrate as the screen the server rendered', async () => {
		const pinia = createPinia()
		usePublicPageStore(pinia).set('user/profile', null)
		const router = createPublicRouter(createMemoryHistory(), pinia, (key) => key)
		await router.push('/user/profile')
		expect(router.currentRoute.value.name).toBe('user-profile')
	})

	it('match the slugs dev SSR serves (and the API mirrors)', () => {
		expect(SCREENS.map((screen) => screen.slug)).toEqual(SCREEN_SLUGS)
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
	it('template.js imports nothing (renderer images copy only it + render-server.js)', () => {
		expect(templateSource).not.toMatch(/^\s*(import|export .* from)\b/m)
	})

	it('keeps $ patterns in content literally', () => {
		const out = splice('<!--app-head--><!--app-html--><!--app-state-->', { head: '$&', html: "$'", state: '$1', tag: 'en' })
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

	it('reads the localized form: default first, then the localization', () => {
		const json = '{ "site": { "lang": { "default": "en-US", "localization": ["de-CH", "fr-CH"] } } }'
		expect(siteLanguages(json)).toEqual(['en-US', 'de-CH', 'fr-CH'])
		expect(siteLang(json)).toBe('en-US')
		expect(siteLanguages('{ "site": { "lang": "de-CH" } }')).toEqual(['de-CH'])
		expect(siteLanguages('{ "site": { "lang": { "localization": ["de"] } } }')).toEqual(['en', 'de'])
	})

	it('rejects a second language with the same code, unknown keys and bad tags', () => {
		const lang = (value: string) => `{ "site": { "lang": ${value} } }`
		expect(() => siteLanguages(lang('{ "default": "en-US", "localization": ["en-GB"] }'))).toThrow(/code 'en'/)
		expect(() => siteLanguages(lang('{ "default": "en", "languages": ["de"] }'))).toThrow(/unknown key 'languages'/)
		expect(() => siteLanguages(lang('{ "default": "en", "localization": ["Deutsch"] }'))).toThrow(/'Deutsch'/)
		expect(() => siteLanguages(lang('{ "default": "en", "localization": "de" }'))).toThrow(/list/)
		expect(() => siteLanguages(lang('5'))).toThrow(/language tag or/)
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

describe('theme colours (build time)', () => {
	it('reads theme.colors, none by default', () => {
		expect(themeColors('{}')).toEqual({})
		expect(themeColors('{ "theme": { "colors": { "primary": "#111", "soft-bg": "oklch(0.97 0 0 / 80%)", }, }, }')).toEqual({
			primary: '#111',
			'soft-bg': 'oklch(0.97 0 0 / 80%)',
		})
	})

	it('rejects bad keys and values that could escape the style tag', () => {
		expect(() => themeColors('{ "theme": { "colors": { "Brand": "#000" } } }')).toThrow(/kebab-case/)
		expect(() => themeColors('{ "theme": { "colors": { "brand": "red" } } }')).toThrow(/theme.colors.brand/)
		expect(() => themeColors('{ "theme": { "colors": { "brand": "#000}</style>" } } }')).toThrow(/not a colour/)
		expect(() => themeColors('{ "theme": { "colors": ["#000"] } }')).toThrow(/must be an object/)
	})

	it('injects the variables into <head>, nothing without colours', () => {
		const style = themeStyle({ primary: '#111', brand: '#0f766e' })
		expect(style).toBe('<style data-cms-theme>:root .site-theme{--color-primary:#111;--color-brand:#0f766e}</style>')
		expect(injectThemeStyle('<head><title>x</title></head>', style)).toBe(`<head><title>x</title>${style}</head>`)
		expect(injectThemeStyle('<head></head>', themeStyle({}))).toBe('<head></head>')
	})
})

describe('languages (localized site: en-US default + de-CH, fr-CH, it-CH)', () => {
	it('renders German strings, prefixed links and the German <html lang>', async () => {
		const rendered = await render(state({ lang: 'de', slug: 'login', page: null }))
		expect(rendered.tag).toBe('de-CH')
		expect(rendered.html).toContain('href="/de/register"')
		expect(rendered.html).toContain('href="/de/contact"') // menu link
		expect(rendered.html).toContain('href="/de/legal"') // footer link
		expect(rendered.html).toContain('Deutsch') // footer language switch
		expect(rendered.html).not.toContain('>Log in<')
		expect(splice('<html lang="en-US"><!--app-html--></html>', rendered)).toMatch(/^<html lang="de-CH">/)
	})

	it('canonical, hreflang for every language + x-default, og:locale', async () => {
		const { head } = await render(state({ lang: 'de' }))
		expect(head).toContain('<link rel="canonical" href="https://site.test/de/about">')
		expect(head).toContain('<link rel="alternate" hreflang="en-US" href="https://site.test/about">')
		expect(head).toContain('<link rel="alternate" hreflang="de-CH" href="https://site.test/de/about">')
		expect(head).toContain('<link rel="alternate" hreflang="fr-CH" href="https://site.test/fr/about">')
		expect(head).toContain('<link rel="alternate" hreflang="it-CH" href="https://site.test/it/about">')
		expect(head).toContain('<link rel="alternate" hreflang="x-default" href="https://site.test/about">')
		expect(head).toContain('<meta property="og:locale" content="de_CH">')
		expect(head).toContain('<meta property="og:locale:alternate" content="en_US">')
		expect(head).not.toContain('og:locale:alternate" content="de_CH"')
	})

	it('home: /de, the default unprefixed', async () => {
		const page = { ...state().page!, slug: 'home' }
		expect((await render(state({ slug: 'home', page, lang: 'fr' }))).head).toContain('href="https://site.test/fr"')
		expect((await render(state({ slug: 'home', page }))).head).toContain('<link rel="canonical" href="https://site.test/">')
	})

	it('router with a language base resolves names and paths with the prefix', () => {
		const router = createPublicRouter(createMemoryHistory(localeBase('de')), createPinia(), (k) => k)
		expect(router.resolve({ name: 'home' }).href).toBe('/de/')
		expect(router.resolve('/login').href).toBe('/de/login')
		expect(router.resolve({ name: 'public-page', params: { slug: 'about' } }).href).toBe('/de/about')
	})

	it('localePath / stripLocale / localeBase', () => {
		expect(localePath('/about', 'de')).toBe('/de/about')
		expect(localePath('/', 'de')).toBe('/de')
		expect(localePath('/about', 'en')).toBe('/about')
		expect(localeBase('en')).toBe('/')
		expect(localeBase('fr')).toBe('/fr/')
		expect(stripLocale('/de/about')).toEqual({ code: 'de', path: '/about' })
		expect(stripLocale('/de')).toEqual({ code: 'de', path: '/' })
		expect(stripLocale('/de/')).toEqual({ code: 'de', path: '/' })
		expect(stripLocale('/de/user/profile')).toEqual({ code: 'de', path: '/user/profile' })
		expect(stripLocale('/about')).toEqual({ code: 'en', path: '/about' })
		expect(stripLocale('/en/about')).toEqual({ code: 'en', path: '/en/about' })
		expect(stripLocale('/es/about')).toEqual({ code: 'en', path: '/es/about' })
		expect(stripLocale('/')).toEqual({ code: 'en', path: '/' })
	})

	it('safeRedirect strips a language prefix', () => {
		expect(safeRedirect('/de/about?x=1#y')).toBe('/about?x=1#y')
		expect(safeRedirect('/de')).toBe('/')
		expect(safeRedirect('/de/admin')).toBeNull()
		expect(safeRedirect('/fr/login')).toBeNull()
	})
})
