import type { Block, PageDetail, PageTextTranslation, SiteConfigResponse } from '@trainpaths/cms/src/api/types.gen.ts'
import { MEDIA, toRef } from './media'

/** The one demo page as first seen (published) plus the site around it: main menu and site config. */

type Lang = 'de' | 'fr' | 'it'
type Texts = Partial<Record<Lang, Record<string, string>>>

let next = 0
function block(name: string, attributes: Block['attributes'], texts: Texts = {}, innerBlocks: Block[] = []): Block {
	const translations = Object.keys(texts).length ? (texts as Block['translations']) : null
	return { id: `demo-${name}-${++next}`, name, attributes, translations, innerBlocks }
}

const heading = (text: string, level: number, texts: Texts = {}, extra: Block['attributes'] = {}) =>
	block('heading', { text, level, ...extra }, texts)
const paragraph = (text: string, texts: Texts = {}, extra: Block['attributes'] = {}) =>
	block('paragraph', { text, ...extra }, texts)
const link = (label: string, url: string, texts: Texts = {}, extra: Block['attributes'] = {}) =>
	block('link', { label, url, ...extra }, texts)
const listItem = (text: string, de: string) => block('list-item', { text }, { de: { text: de } })

const STARTER = 'https://github.com/trainpaths/cms-starter'
const REPO = 'https://github.com/trainpaths/cms'

export function seedBlocks(): Block[] {
	next = 0
	return [
		heading(
			'Websites built from blocks',
			2,
			{
				de: { text: 'Websites aus Blöcken gebaut' },
				fr: { text: 'Des sites web construits en blocs' },
				it: { text: 'Siti web costruiti a blocchi' },
			},
			{ blockWidth: 'wide' },
		),
		paragraph(
			'A website its owner can keep up to date without a developer. Every page is a stack of blocks: text, images, lists, cards and links, each with its own settings.',
			{
				de: {
					text: 'Eine Website, die ihre Besitzer ohne Entwickler aktuell halten können. Jede Seite ist ein Stapel aus Blöcken: Text, Bilder, Listen, Karten und Links, jeder mit eigenen Einstellungen.',
				},
				fr: {
					text: 'Un site web que son propriétaire peut tenir à jour sans développeur. Chaque page est une pile de blocs : textes, images, listes, cartes et liens.',
				},
				it: {
					text: 'Un sito web che il proprietario può aggiornare senza uno sviluppatore. Ogni pagina è una pila di blocchi: testi, immagini, elenchi, schede e link.',
				},
			},
			{ alignment: 'center' },
		),
		block(
			'image',
			{ mediaId: 'media-lake', caption: 'Full width: the widest a block can go.', width: 100, blockWidth: 'full' },
			{ de: { caption: 'Volle Breite: so breit, wie ein Block werden kann.' } },
		),
		block(
			'callout',
			{
				text: 'This is a live demo. Open the editor with the Demo button, change anything and publish it. Your changes stay in this browser only.',
				tone: 'info',
			},
			{
				de: {
					text: 'Dies ist eine Live-Demo. Öffne den Editor mit dem Demo-Knopf, ändere etwas und veröffentliche es. Deine Änderungen bleiben nur in diesem Browser.',
				},
			},
		),
		heading('What you get', 2, { de: { text: 'Was du bekommst' }, fr: { text: 'Ce que vous obtenez' } }),
		block(
			'card',
			{
				title: 'Design',
				description: 'Layouts, colours and type that fit your brand. The site theme sets the palette the editor offers.',
				mediaId: 'media-mountains',
			},
			{
				de: {
					title: 'Design',
					description:
						'Layouts, Farben und Schriften, die zu deiner Marke passen. Das Site-Theme bestimmt die Farbpalette im Editor.',
				},
			},
			[link('See the starter template', STARTER, { de: { label: 'Zur Starter-Vorlage' } })],
		),
		block(
			'card',
			{
				title: 'Development',
				description: 'Your own blocks and page templates next to the built-in ones, rendered on the server for fast pages.',
				mediaId: 'media-city',
				backgroundColor: 'light',
			},
			{
				de: {
					title: 'Entwicklung',
					description:
						'Eigene Blöcke und Seitenvorlagen neben den eingebauten, auf dem Server gerendert für schnelle Seiten.',
				},
			},
			[link('Read the source', REPO, { de: { label: 'Zum Quellcode' } })],
		),
		block(
			'card',
			{
				title: 'Content',
				description:
					'Draft, preview and publish when you are ready. Translations live next to the original text, block by block.',
				backgroundColor: 'primary',
				textColor: 'white',
				blockWidth: 'wide',
			},
			{
				de: {
					title: 'Inhalt',
					description:
						'Entwerfen, ansehen und veröffentlichen, wenn du bereit bist. Übersetzungen stehen Block für Block neben dem Originaltext.',
				},
			},
			[link('Try it in the editor', '/edit', { de: { label: 'Im Editor ausprobieren' } }, { textColor: 'white' })],
		),
		heading('How it works', 2, { de: { text: 'So funktioniert es' } }),
		block('list', { ordered: true }, {}, [
			listItem('Add blocks from the sidebar or with the + buttons.', 'Blöcke aus der Seitenleiste oder mit den +-Knöpfen hinzufügen.'),
			listItem('Drag them into place and set width and colours.', 'An die richtige Stelle ziehen und Breite und Farben wählen.'),
			listItem('Save a draft, look at it, then publish.', 'Einen Entwurf speichern, ansehen, dann veröffentlichen.'),
		]),
		block(
			'image',
			{ mediaId: 'media-forest', caption: 'Wide: a little wider than the text column.', width: 100, blockWidth: 'wide' },
			{ de: { caption: 'Breit: etwas breiter als die Textspalte.' } },
		),
		heading('Why blocks?', 3, { de: { text: 'Warum Blöcke?' } }),
		block('list', { ordered: false }, {}, [
			listItem('Every block looks the same in the editor and on the site.', 'Jeder Block sieht im Editor und auf der Website gleich aus.'),
			listItem('Undo and redo for every change.', 'Rückgängig und Wiederholen für jede Änderung.'),
			listItem('Works on phones and tablets too.', 'Funktioniert auch auf Handys und Tablets.'),
			listItem('Your developer can add blocks of their own.', 'Dein Entwicklungsteam kann eigene Blöcke hinzufügen.'),
		]),
		paragraph(
			'Paragraphs can be aligned and coloured with the site palette, like this one.',
			{ de: { text: 'Absätze lassen sich ausrichten und mit der Farbpalette der Website einfärben, wie dieser hier.' } },
			{ alignment: 'center', backgroundColor: 'light', textColor: 'dark', blockWidth: 'wide' },
		),
		block(
			'image',
			{ mediaId: 'media-blocks', caption: 'Images can be narrower than the column too (60% here).', width: 60 },
			{ de: { caption: 'Bilder können auch schmaler als die Spalte sein (hier 60 %).' } },
		),
		block(
			'callout',
			{
				text: 'Callouts like this one are a custom block of this site, added next to the built-in ones.',
				tone: 'warning',
			},
			{
				de: {
					text: 'Hinweise wie dieser sind ein eigener Block dieser Website, neben den eingebauten Blöcken.',
				},
			},
		),
		heading(
			'Get in touch',
			2,
			{ de: { text: 'Kontakt' } },
			{ backgroundColor: 'accent', textColor: 'dark', blockWidth: 'wide' },
		),
		paragraph(
			'Want a site like this? Start from the template, or read how the CMS works on GitHub.',
			{
				de: {
					text: 'Du willst eine Website wie diese? Starte mit der Vorlage oder lies auf GitHub, wie das CMS funktioniert.',
				},
			},
		),
		link('Start with the cms-starter template', STARTER, { de: { label: 'Mit der cms-starter-Vorlage beginnen' } }),
		link('The CMS on GitHub', REPO, { de: { label: 'Das CMS auf GitHub' } }, { textColor: 'primary' }),
	]
}

const translations: Record<string, PageTextTranslation> = {
	de: {
		title: 'Willkommen',
		metaTitle: 'Websites aus Blöcken',
		metaDescription: 'Eine Demo des Block-Editors: bearbeiten, ansehen, veröffentlichen.',
	},
	fr: { title: 'Bienvenue', metaTitle: '', metaDescription: '' },
	it: { title: 'Benvenuti', metaTitle: '', metaDescription: '' },
}

export const DEMO_PAGE_ID = 'demo-page'
const CREATED = '2026-01-05T09:00:00Z'

export function seedPage(): PageDetail {
	return {
		id: DEMO_PAGE_ID,
		title: 'Welcome',
		slug: 'home',
		status: 'published',
		tags: ['demo'],
		blocks: seedBlocks(),
		media: MEDIA.map(toRef),
		createdAt: CREATED,
		updatedAt: CREATED,
		publishedAt: CREATED,
		hasUnpublishedChanges: false,
		template: null,
		// the only page: slug fixed (it is the home page), like a locked config page
		locked: true,
		editable: true,
		metaTitle: 'Websites built from blocks',
		metaDescription: 'A demo of the block editor: edit, view and publish a page.',
		translations: structuredClone(translations),
	}
}

/** Main menu labels per language (default first); `slug` = the demo page, `url` = external. */
export interface DemoMenuItem {
	label: Record<string, string>
	slug?: string
	url?: string
	children?: DemoMenuItem[]
}

export const MENU: DemoMenuItem[] = [
	{ label: { en: 'Home', de: 'Start', fr: 'Accueil', it: 'Home' }, slug: 'home' },
	{
		label: { en: 'Get the CMS', de: 'Das CMS', fr: 'Le CMS', it: 'Il CMS' },
		children: [
			{ label: { en: 'Starter template', de: 'Starter-Vorlage', fr: 'Modèle de départ', it: 'Modello iniziale' }, url: STARTER },
			{ label: { en: 'Source code', de: 'Quellcode', fr: 'Code source', it: 'Codice sorgente' }, url: REPO },
		],
	},
	{ label: { en: 'GitHub', de: 'GitHub', fr: 'GitHub', it: 'GitHub' }, url: 'https://github.com/trainpaths' },
]

export const SITE_CONFIG: SiteConfigResponse = {
	fields: {},
	groups: {
		contact: [
			{
				id: 'contact-address',
				key: 'address',
				label: 'Address',
				type: 'address',
				value: '',
				address: { street: 'Example Street 1', postalCode: '1234', city: 'Sample Town', country: 'Exampleland' },
			},
			{ id: 'contact-phone', key: 'phone', label: 'Phone', type: 'phone', value: '+41 44 000 00 00' },
			{ id: 'contact-email', key: 'email', label: 'Email', type: 'email', value: 'hello@example.com' },
		],
		socials: [],
	},
	logo: null,
	icon: null,
	shareImage: null,
	updatedAt: CREATED,
	footerLinks: [],
}
