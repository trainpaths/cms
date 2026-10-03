import type { ComputedRef, InjectionKey, Ref } from 'vue'
import { nanoid } from 'nanoid'
import type { MenuItem, PageSummary } from '../../lib/web-editor'
import type { MenuDropTarget } from './useMenuDrag'

/** Shared by the tree's lists, rows and drop slots (provided by `MenuTree`). */
export interface MenuTreeContext {
	root: Ref<MenuItem[]>
	/** by id */
	pages: ComputedRef<Map<string, PageSummary>>
	/** by link path ("/about") */
	pagePaths: ComputedRef<Map<string, PageSummary>>
	errors: ComputedRef<Map<string, string>>
	/** Whether the current drag may land on `target`. */
	accepts: (target: MenuDropTarget) => boolean
	/** Applies the current drag at the current drop target. */
	drop: () => void
}

export const menuTreeKey: InjectionKey<MenuTreeContext> = Symbol('menuTree')

/** Link path shown for a page. */
export const pagePath = (page: PageSummary) => `/${page.slug}`

export const newItem = (fields: Partial<MenuItem> = {}): MenuItem => ({
	id: nanoid(10),
	label: '',
	pageId: null,
	url: null,
	children: [],
	...fields,
})

/** A page added from the picker (click or drag): label = title, link = the page. */
export const pageItem = (page: PageSummary) => newItem({ label: page.title, pageId: page.id })
