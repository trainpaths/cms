<script setup lang="ts">
import { computed, inject } from 'vue'
import { useEditorStore } from '../../../stores/editor'
import { getBlockType } from '../core/blockRegistry'
import type { BlockInstance } from '../core/types'
import { outlineKey } from './outline'
import { scrollBlockIntoView } from './scrollBlockIntoView'

const props = defineProps<{ block: BlockInstance }>()

const store = useEditorStore()
const outline = inject(outlineKey)!

const type = computed(() => getBlockType(props.block.name))
const hasChildren = computed(() => props.block.innerBlocks.length > 0)
const expanded = computed(() => !outline.collapsed.value.has(props.block.id))
const selected = computed(() => store.selectedBlockId === props.block.id)

// first text-like attribute, whitespace collapsed; CSS truncates
const snippet = computed(() => {
	const { text, title, label } = props.block.attributes
	const value = [text, title, label].find((v) => typeof v === 'string' && v.trim())
	return (value as string | undefined)?.replace(/\s+/g, ' ').trim() ?? ''
})

async function select() {
	store.selectBlock(props.block.id)
	store.closeInserterOnMobile()
	await scrollBlockIntoView(props.block.id)
}
</script>

<template>
	<li>
		<div
			class="flex items-center rounded-md"
			:class="selected ? 'bg-primary/10 text-primary' : 'text-gray-700 hover:bg-gray-100'"
		>
			<button
				v-if="hasChildren"
				type="button"
				class="flex size-20 shrink-0 cursor-pointer items-center justify-center rounded border-none bg-transparent p-0 text-gray-400 hover:text-gray-700"
				:aria-expanded="expanded"
				:aria-label="expanded ? 'Collapse' : 'Expand'"
				@click="outline.toggle(block.id)"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					width="12"
					height="12"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2.5"
					stroke-linecap="round"
					stroke-linejoin="round"
					class="transition-transform"
					:class="{ 'rotate-90': expanded }"
				>
					<polyline points="9 6 15 12 9 18" />
				</svg>
			</button>
			<span
				v-else
				class="size-20 shrink-0"
			/>
			<button
				type="button"
				class="flex min-w-0 flex-1 cursor-pointer items-center gap-8 border-none bg-transparent px-4 py-6 text-left text-inherit"
				:aria-current="selected || undefined"
				:data-outline-id="block.id"
				data-testid="outline-item"
				@click="select"
			>
				<span class="flex size-16 shrink-0 items-center justify-center text-gray-500">
					<component
						:is="type?.icon"
						v-if="type"
						class="h-full w-full"
					/>
				</span>
				<span class="shrink-0 text-sm font-medium">{{ type?.title ?? block.name }}</span>
				<span
					v-if="snippet"
					class="min-w-0 truncate text-xs text-gray-400"
					>{{ snippet }}</span
				>
			</button>
		</div>
		<ul
			v-if="hasChildren && expanded"
			class="m-0 list-none py-0 pl-16 pr-0"
		>
			<BlockOutlineItem
				v-for="child in block.innerBlocks"
				:key="child.id"
				:block="child"
			/>
		</ul>
	</li>
</template>
