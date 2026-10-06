<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import { Button, Icon, type IconName } from '@trainpaths/nb-ui'

export interface ActionItem {
	label: string
	icon: IconName
	danger?: boolean
	disabled?: boolean
	/** tooltip, e.g. why an item is disabled */
	title?: string
	testId?: string
	onSelect: () => void
}

/**
 * "…" button with a small menu below it. Same look and keyboard handling as nb-ui's `Dropdown`, plus a per-item
 * `title` (why an item is disabled) and `data-testid`, which `Dropdown` items don't take.
 */
withDefaults(defineProps<{ items: ActionItem[]; label?: string; testId?: string }>(), {
	label: 'Actions',
	testId: undefined,
})

const open = ref(false)
const root = ref<HTMLElement | null>(null)
const menu = ref<HTMLElement | null>(null)
const menuId = `action-menu-${useId()}`

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
				class="absolute top-full right-0 z-40 mt-4 flex min-w-120 origin-top-right flex-col rounded-md border border-gray-200 bg-white p-4 font-sans shadow-lg outline-hidden"
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
					<Icon :name="item.icon" />
					{{ item.label }}
				</button>
			</div>
		</Transition>
	</div>
</template>
