import type { Block, PageDetail, PageTextTranslation } from '@trainpaths/cms/src/api/types.gen.ts'
import { DEMO_PAGE_ID, seedPage } from './seed'

/** What visitors see: the working copy as it was at the last publish. */
export interface LiveSnapshot {
	title: string
	slug: string
	blocks: Block[]
	metaTitle: string
	metaDescription: string
	translations: Record<string, PageTextTranslation>
	publishedAt: string
}

export interface DemoData {
	/** working copy (what the editor saves) */
	page: PageDetail
	live: LiveSnapshot | null
}

export interface DemoStore {
	readonly data: DemoData
	/** persists `data`; false when the browser refused (storage full or blocked) */
	save(): boolean
	reset(): void
}

const KEY = 'cms-demo:v1'

export function snapshot(page: PageDetail, publishedAt: string): LiveSnapshot {
	return structuredClone({
		title: page.title,
		slug: page.slug,
		blocks: page.blocks,
		metaTitle: page.metaTitle,
		metaDescription: page.metaDescription,
		translations: page.translations,
		publishedAt,
	})
}

export function seedData(): DemoData {
	const page = seedPage()
	return { page, live: snapshot(page, page.publishedAt!) }
}

function read(storage: Storage | null): DemoData | null {
	try {
		const raw = storage?.getItem(KEY)
		if (!raw) return null
		const data = JSON.parse(raw) as DemoData
		return data?.page?.id === DEMO_PAGE_ID && Array.isArray(data.page.blocks) ? data : null
	} catch {
		return null
	}
}

function browserStorage(): Storage | null {
	try {
		return typeof localStorage === 'undefined' ? null : localStorage
	} catch {
		return null
	}
}

/** The demo's one page, kept in localStorage (memory only when the browser doesn't allow it). */
export function createDemoStore(storage: Storage | null = browserStorage()): DemoStore {
	let data = read(storage) ?? seedData()
	return {
		get data() {
			return data
		},
		save() {
			try {
				storage?.setItem(KEY, JSON.stringify(data))
				return true
			} catch {
				return false
			}
		},
		reset() {
			data = seedData()
			try {
				storage?.removeItem(KEY)
			} catch {
				// memory copy is reset either way
			}
		},
	}
}
