<script setup lang="ts">
import { computed, inject, ref } from 'vue'
import { MENU_LABEL_MAX, MENU_MAX_DEPTH, MENU_URL_MAX, type MenuItem } from '../../lib/web-editor'
import { height, indent, locate, moveBy, outdent, remove } from '../../lib/menus/tree'
import MenuList from './MenuList.vue'
import { menuTreeKey, pagePath } from './context'
import { useMenuDrag, type MenuDropTarget } from './useMenuDrag'

const props = defineProps<{
	item: MenuItem
	index: number
	siblings: number
	depth: number
	parentId: string | null
}>()
const ctx = inject(menuTreeKey)!
const { dragged, dropTarget, start, end, isInside } = useMenuDrag()
const row = ref<HTMLElement | null>(null)

const page = computed(() => (props.item.pageId ? ctx.pages.value.get(props.item.pageId) : undefined))
// page links show the page's path; typing a known page's path links that page (follows later slug changes)
const link = computed(() => (props.item.pageId ? (page.value ? pagePath(page.value) : '') : (props.item.url ?? '')))
const error = computed(() => ctx.errors.value.get(props.item.id))
const canIndent = computed(() => props.index > 0 && props.depth - 1 + height(props.item) < MENU_MAX_DEPTH)

// edits go through the root tree, not the prop
function set(patch: Partial<MenuItem>) {
	const loc = locate(ctx.root.value, props.item.id)
	if (loc) Object.assign(loc.item, patch)
}

function setLink(value: string) {
	const match = ctx.pagePaths.value.get(value.trim())
	if (match) set({ pageId: match.id, url: null })
	else set({ pageId: null, url: value.trim() ? value : null })
}

// top third = before, bottom third = after (first child when it has children, right below the row), middle = inside
function onDragOver(e: DragEvent) {
	if (!dragged.value || !row.value) return
	e.stopPropagation()
	const rect = row.value.getBoundingClientRect()
	const y = (e.clientY - rect.top) / rect.height
	const target: MenuDropTarget =
		y < 1 / 3
			? { kind: 'slot', parentId: props.parentId, index: props.index }
			: y > 2 / 3
				? props.item.children.length
					? { kind: 'slot', parentId: props.item.id, index: 0 }
					: { kind: 'slot', parentId: props.parentId, index: props.index + 1 }
				: { kind: 'inside', id: props.item.id }
	if (!ctx.accepts(target)) {
		dropTarget.value = null
		return
	}
	e.preventDefault()
	dropTarget.value = target
}

function onDrop(e: DragEvent) {
	e.preventDefault()
	e.stopPropagation()
	ctx.drop()
}

const btn =
	'cursor-pointer rounded border-none bg-transparent px-6 py-2 text-gray-500 hover:bg-gray-100 hover:text-gray-900 disabled:cursor-default disabled:opacity-30 disabled:hover:bg-transparent'
</script>

<template>
	<li
		class="m-0"
		data-testid="menu-item"
		:data-depth="depth"
	>
		<div
			ref="row"
			class="flex flex-col gap-6 rounded-md border border-gray-400 bg-white p-6 transition-shadow md:flex-row md:items-center"
			:class="[
				isInside(item.id) ? 'shadow-[0_4px_0_var(--color-primary)]' : '',
				dragged?.kind === 'move' && dragged.id === item.id ? 'opacity-50' : '',
			]"
			:data-drop-inside="isInside(item.id) || undefined"
			data-testid="menu-item-row"
			@dragover="onDragOver"
			@drop="onDrop"
		>
			<div class="flex min-w-0 flex-1 items-center gap-6">
				<span
					draggable="true"
					class="cursor-grab select-none px-4 text-gray-500 hover:text-gray-900"
					title="Drag to move"
					aria-hidden="true"
					data-testid="menu-item-grip"
					@dragstart="start($event, { kind: 'move', id: item.id }, row)"
					@dragend="end"
				>
					&#10303;
				</span>
				<input
					:value="item.label"
					type="text"
					class="input-field mt-0 min-w-0 flex-1"
					:class="{ 'border-red-400': error && !item.label.trim() }"
					:maxlength="MENU_LABEL_MAX"
					:placeholder="page?.title ?? 'Label'"
					aria-label="Label"
					data-testid="menu-item-label"
					@input="set({ label: ($event.target as HTMLInputElement).value })"
				/>
			</div>
			<div class="flex min-w-0 flex-1 items-center gap-6">
				<input
					:value="link"
					type="text"
					class="input-field mt-0 min-w-0 flex-1"
					:class="{ 'border-red-400': error && item.url }"
					:maxlength="MENU_URL_MAX"
					placeholder="Link (optional)"
					aria-label="Link"
					data-testid="menu-item-link"
					@input="setLink(($event.target as HTMLInputElement).value)"
				/>
				<span
					v-if="page && page.status !== 'published'"
					class="shrink-0 rounded-full bg-yellow-100 px-6 py-1 text-[11px] text-yellow-800"
					title="Draft pages are left out of the public menu"
					>Draft</span
				>
				<span
					v-if="item.pageId && !page"
					class="shrink-0 rounded-full bg-red-100 px-6 py-1 text-[11px] text-red-700"
					>Page deleted</span
				>
				<div class="flex shrink-0 items-center">
					<button
						type="button"
						:class="btn"
						:disabled="index === 0"
						aria-label="Move up"
						@click="moveBy(ctx.root.value, item.id, -1)"
					>
						&#8593;
					</button>
					<button
						type="button"
						:class="btn"
						:disabled="index === siblings - 1"
						aria-label="Move down"
						@click="moveBy(ctx.root.value, item.id, 1)"
					>
						&#8595;
					</button>
					<button
						type="button"
						:class="btn"
						:disabled="depth === 1"
						aria-label="Outdent"
						data-testid="menu-item-outdent"
						@click="outdent(ctx.root.value, item.id)"
					>
						&#8676;
					</button>
					<button
						type="button"
						:class="btn"
						:disabled="!canIndent"
						aria-label="Indent into previous item"
						data-testid="menu-item-indent"
						@click="indent(ctx.root.value, item.id, MENU_MAX_DEPTH)"
					>
						&#8677;
					</button>
					<button
						type="button"
						:class="[btn, 'hover:bg-red-50 hover:text-red-600']"
						:aria-label="item.children.length ? 'Remove item and its children' : 'Remove item'"
						data-testid="menu-item-remove"
						@click="remove(ctx.root.value, item.id)"
					>
						&#10005;
					</button>
				</div>
			</div>
		</div>
		<p
			v-if="error"
			class="m-0 mt-2 pl-24 text-xs text-red-600"
		>
			{{ error }}
		</p>
		<MenuList
			v-if="item.children.length"
			:items="item.children"
			:parent-id="item.id"
			:depth="depth + 1"
			class="ml-12 border-l-2 border-gray-400 pl-16"
		/>
	</li>
</template>
