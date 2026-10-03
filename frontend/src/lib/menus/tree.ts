import type { MenuItem } from '../web-editor'

/**
 * Pure operations on a menu item tree (mutate in place). Shared by the menu editor's buttons and its
 * drag & drop. Depth is 1-based: root items are depth 1, the root list itself is depth 0.
 */

interface Location {
	item: MenuItem
	list: MenuItem[]
	index: number
	parent: MenuItem | null
	depth: number
}

export function locate(items: MenuItem[], id: string, parent: MenuItem | null = null, depth = 1): Location | null {
	for (let index = 0; index < items.length; index++) {
		const item = items[index]!
		if (item.id === id) return { item, list: items, index, parent, depth }
		const found = locate(item.children, id, item, depth + 1)
		if (found) return found
	}
	return null
}

export const countItems = (items: MenuItem[]): number => items.reduce((n, i) => n + 1 + countItems(i.children), 0)

/** Levels an item spans including itself (leaf = 1). */
export const height = (item: MenuItem): number => 1 + Math.max(0, ...item.children.map(height))

export const contains = (item: MenuItem, id: string): boolean =>
	item.children.some((c) => c.id === id || contains(c, id))

/** Children list of `parentId` (root list when null). */
function listOf(items: MenuItem[], parentId: string | null): { list: MenuItem[]; depth: number } | null {
	if (!parentId) return { list: items, depth: 0 }
	const parent = locate(items, parentId)
	return parent ? { list: parent.item.children, depth: parent.depth } : null
}

/**
 * Whether something `spanning` levels tall may go into `parentId`'s children without exceeding `maxDepth`;
 * with `movingId`, also that it isn't moved into itself or its own subtree.
 */
export function canInsert(
	items: MenuItem[],
	parentId: string | null,
	spanning: number,
	maxDepth: number,
	movingId?: string,
): boolean {
	const target = listOf(items, parentId)
	if (!target) return false
	if (movingId && parentId) {
		const moving = locate(items, movingId)
		if (!moving || parentId === movingId || contains(moving.item, parentId)) return false
	}
	return target.depth + spanning <= maxDepth
}

export function insertAt(items: MenuItem[], parentId: string | null, index: number, item: MenuItem): void {
	listOf(items, parentId)?.list.splice(index, 0, item)
}

/** Moves `id` to position `index` of `parentId`'s children (index as seen before the move). */
export function moveTo(items: MenuItem[], id: string, parentId: string | null, index: number): void {
	const source = locate(items, id)
	const target = listOf(items, parentId)
	if (!source || !target) return
	source.list.splice(source.index, 1)
	// same list, moving down: removal shifted the target slot up by one
	const at = target.list === source.list && source.index < index ? index - 1 : index
	target.list.splice(at, 0, source.item)
}

export function moveBy(items: MenuItem[], id: string, delta: -1 | 1): void {
	const loc = locate(items, id)
	if (!loc) return
	const to = loc.index + delta
	if (to < 0 || to >= loc.list.length) return
	loc.list.splice(loc.index, 1)
	loc.list.splice(to, 0, loc.item)
}

/** Becomes the last child of its previous sibling. */
export function indent(items: MenuItem[], id: string, maxDepth: number): void {
	const loc = locate(items, id)
	const previous = loc && loc.list[loc.index - 1]
	if (!loc || !previous || loc.depth + height(loc.item) > maxDepth) return
	loc.list.splice(loc.index, 1)
	previous.children.push(loc.item)
}

/** Moves out of its parent, right after it. */
export function outdent(items: MenuItem[], id: string): void {
	const loc = locate(items, id)
	if (!loc?.parent) return
	const parentLoc = locate(items, loc.parent.id)!
	loc.list.splice(loc.index, 1)
	parentLoc.list.splice(parentLoc.index + 1, 0, loc.item)
}

export function remove(items: MenuItem[], id: string): void {
	const loc = locate(items, id)
	loc?.list.splice(loc.index, 1)
}
