import { describe, expect, it } from 'vitest'
import { computed, createSSRApp, defineComponent, h } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { useI18n } from 'vue-i18n'
import {
	createCmsI18n,
	DEFAULT_LANGUAGE,
	LANGUAGE_SWITCH,
	LANGUAGES,
	langFromCookie,
	languageLabel,
	languageOf,
	loadAdminMessages,
	type Language,
} from '.'
import { useConfigLabels } from './siteConfigLabels'
import LanguageDropdown from '../components/LanguageDropdown.vue'

type Messages = { [key: string]: string | Messages }
const files = import.meta.glob<Messages>('../locales/*/*.json', { eager: true, import: 'default' })

const keys = (messages: Messages, prefix = ''): string[] =>
	Object.entries(messages).flatMap(([key, value]) =>
		typeof value === 'string' ? [prefix + key] : keys(value, `${prefix}${key}.`),
	)

describe('languages (vitest defines the playground list)', () => {
	it('are the tags of the config, default first, with their codes', () => {
		expect(LANGUAGES.map((l) => l.code)).toEqual(['en', 'de', 'fr', 'it'])
		expect(DEFAULT_LANGUAGE).toEqual({ code: 'en', tag: 'en-US' })
		expect(languageOf('fr').tag).toBe('fr-CH')
		expect(languageOf('es')).toBe(DEFAULT_LANGUAGE)
	})

	it('are named in themselves', () => {
		expect(['en', 'de', 'fr', 'it'].map(languageLabel)).toEqual(['English', 'Deutsch', 'Français', 'Italiano'])
	})

	it('read the cms_lang cookie, configured languages only', () => {
		expect(langFromCookie('a=1; cms_lang=de; b=2')?.tag).toBe('de-CH')
		expect(langFromCookie('cms_lang=es')).toBeUndefined()
		expect(langFromCookie('')).toBeUndefined()
	})
})

describe('messages', () => {
	for (const area of ['public', 'admin']) {
		const en = keys(files[`../locales/${area}/en.json`]!).sort()
		for (const code of ['de', 'fr', 'it']) {
			it(`${area}/${code}.json has exactly the English keys`, () => {
				expect(keys(files[`../locales/${area}/${code}.json`]!).sort()).toEqual(en)
			})
		}
	}

	it('compile in every language (no syntax errors, no missing keys)', async () => {
		for (const language of LANGUAGES) {
			const i18n = createCmsI18n(language)
			await loadAdminMessages(i18n, language.code)
			const missing: string[] = []
			i18n.global.setMissingHandler((_locale, key) => void missing.push(key))
			for (const key of keys(files['../locales/admin/en.json']!).concat(keys(files['../locales/public/en.json']!))) {
				const text = i18n.global.t(key, { n: 2, count: 2, name: 'x', slug: 'x', title: 'x' }, 2)
				expect(text, `${language.code}: ${key}`).not.toBe(key)
			}
			expect(missing).toEqual([])
		}
	})

	it('pluralize and interpolate', () => {
		const i18n = createCmsI18n(languageOf('de'))
		expect(i18n.global.t('screens.login.submit')).toBe('Anmelden')
		expect(i18n.global.t('site.submenu', { label: 'Team' })).toBe('Untermenü Team')
	})
})

/** Renders `render` inside an SSR app with the given language switch. */
async function ssr(render: () => ReturnType<typeof h>, languages: readonly Language[], current = languages[0]!) {
	const app = createSSRApp(defineComponent({ setup: () => render }))
	app.use(createCmsI18n(current))
	app.provide(LANGUAGE_SWITCH, { current: computed(() => current), languages, change: () => {} })
	return renderToString(app)
}

describe('LanguageDropdown', () => {
	it('shows the current language as the trigger: flag + short code, its name in the label', async () => {
		const html = await ssr(() => h(LanguageDropdown), LANGUAGES, languageOf('fr'))
		expect(html).toContain('data-testid="language-dropdown"')
		expect(html).toContain('aria-label="Langue: Français"')
		expect(html).toMatch(/<\/svg> FR/)
	})

	it('renders nothing on a single-language site', async () => {
		const html = await ssr(() => h(LanguageDropdown), [DEFAULT_LANGUAGE])
		expect(html).not.toContain('language-dropdown')
	})
})

describe('site config labels', () => {
	const Labels = defineComponent({
		props: { label: { type: String, required: true } },
		setup(props) {
			const labels = useConfigLabels()
			const { t } = useI18n()
			return () => h('span', `${labels.field('firmName', props.label)}|${labels.field('vatId', 'VAT ID')}|${t('common.save')}`)
		},
	})

	it('translate the core names, keep the developer’s own wording and unknown keys', async () => {
		const de = [languageOf('de')]
		expect(await ssr(() => h(Labels, { label: 'Firm name' }), de)).toContain('Firmenname|VAT ID|Speichern')
		expect(await ssr(() => h(Labels, { label: 'Company' }), de)).toContain('Company|VAT ID')
	})
})
