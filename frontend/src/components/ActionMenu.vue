<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import { Button, Icon } from '@trainpaths/nb-ui'

export type ActionIcon = 'edit' | 'copy' | 'download' | 'trash'

export interface ActionItem {
	label: string
	icon: ActionIcon
	danger?: boolean
	disabled?: boolean
	/** tooltip, e.g. why an item is disabled */
	title?: string
	testId?: string
	onSelect: () => void
}

/**
 * "…" button with a small menu below it. Same look and keyboard handling as nb-ui's `Dropdown`, which only renders
 * nb-ui icon names (no edit/copy/download/trash there); this one draws its own icons.
 */
withDefaults(defineProps<{ items: ActionItem[]; label?: string; testId?: string }>(), {
	label: 'Actions',
	testId: undefined,
})

const open = ref(false)
const root = ref<HTMLElement | null>(null)
const menu = ref<HTMLElement | null>(null)
const menuId = `action-menu-${useId()}`

// lucide paths (same set as the other inline icons in the admin)
const icons: Record<ActionIcon, string[]> = {
	edit: ['M12 20h9', 'M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z'],
	copy: [
		'M11 9h9a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-9a2 2 0 0 1-2-2v-9a2 2 0 0 1 2-2z',
		'M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1',
	],
	download: ['M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4', 'M7 10l5 5 5-5', 'M12 15V3'],
	trash: ['M3 6h18', 'M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2'],
}

const menuItems = () => [...(menu.value?.querySelectorAll<HTMLElement>('[role="menuitem"]:not([disabled])') ?? [])]

async function show(focus: 'first' | 'last') {
	open.value = true
	await nextTick()
	const items = menuItems()
	;(focus === 'first' ? items[0] : items[items.length - 1])?.focus()
}

function hide(refocus = true) {
	open.value = false
	if (refocus) root.value?.querySelector<HTMLElement>('[aria-haspopup]')?.focus()
}

function select(item: ActionItem) {
	if (item.disabled) return
	hide(false)
	item.onSelect()
}

function onTriggerKeydown(e: KeyboardEvent) {
	if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
		e.preventDefault()
		show(e.key === 'ArrowDown' ? 'first' : 'last')
	}
}

function onMenuKeydown(e: KeyboardEvent) {
	const items = menuItems()
	const i = items.indexOf(document.activeElement as HTMLElement)
	const move: Record<string, number> = {
		ArrowDown: (i + 1) % items.length,
		ArrowUp: (i - 1 + items.length) % items.length,
		Home: 0,
		End: items.length - 1,
	}
	if (e.key in move) {
		e.preventDefault()
		items[move[e.key]!]?.focus()
	} else if (e.key === 'Escape') {
		e.stopPropagation()
		hide()
	} else if (e.key === 'Tab') {
		hide(false)
	}
}

function onOutside(e: PointerEvent) {
	if (!root.value?.contains(e.target as Node)) hide(false)
}

watch(open, (o) => {
	if (o) document.addEventListener('pointerdown', onOutside)
	else document.removeEventListener('pointerdown', onOutside)
})
onBeforeUnmount(() => document.removeEventListener('pointerdown', onOutside))
</script>

<template>
	<div
		ref="root"
		class="relative inline-block"
	>
		<Button
			variant="ghost"
			text="secondary"
			square
			:title="label"
			:aria-label="label"
			aria-haspopup="menu"
			:aria-expanded="open"
			:aria-controls="menuId"
			:data-testid="testId"
			@click="open ? hide() : show('first')"
			@keydown="onTriggerKeydown"
		>
			<Icon name="more" />
		</Button>
		<Transition
			enter-active-class="transition duration-100 ease-out motion-reduce:transition-none"
			enter-from-class="opacity-0 scale-95"
			leave-active-class="transition duration-75 ease-in motion-reduce:transition-none"
			leave-to-class="opacity-0 scale-95"
		>
			<div
				v-if="open"
				:id="menuId"
				ref="menu"
				role="menu"
				tabindex="-1"
				:aria-label="label"
				class="absolute top-full right-0 z-40 mt-4 flex min-w-140 origin-top-right flex-col rounded-md border border-gray-200 bg-white p-4 font-sans shadow-lg outline-hidden"
				@keydown="onMenuKeydown"
			>
				<button
					v-for="item in items"
					:key="item.label"
					type="button"
					role="menuitem"
					tabindex="-1"
					:disabled="item.disabled"
					:title="item.title"
					:data-testid="item.testId"
					:class="[
						'flex w-full cursor-pointer items-center gap-8 rounded-sm border-none bg-transparent px-8 py-6 text-left text-sm whitespace-nowrap focus:outline-hidden disabled:cursor-not-allowed disabled:opacity-50',
						item.danger
							? 'text-danger hover:bg-danger/10 focus:bg-danger/10'
							: 'text-black hover:bg-accent/20 focus:bg-accent/20',
					]"
					@click="select(item)"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						width="16"
						height="16"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						stroke-linecap="round"
						stroke-linejoin="round"
						aria-hidden="true"
					>
						<path
							v-for="d in icons[item.icon]"
							:key="d"
							:d="d"
						/>
					</svg>
					{{ item.label }}
				</button>
			</div>
		</Transition>
	</div>
</template>
