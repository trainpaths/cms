<script setup lang="ts">
import { ref, computed } from 'vue'
import { getRegisteredBlockTypes, getBlockType, createBlockInstance } from '../core/blockRegistry'
import { useEditorStore } from '../../../stores/editor'
import { useInstanceStore } from '../../../stores/instance'
import type { BlockCategory, BlockType } from '../core/types'
import { useBlockDrag } from '../composables/useBlockDrag'
import { scrollBlockIntoView } from './scrollBlockIntoView'

const store = useEditorStore()
const { startDrag, endDrag } = useBlockDrag()
const searchQuery = ref('')

const categories: BlockCategory[] = ['text', 'media', 'containers', 'embeds']

const expanded = ref<Record<string, boolean>>({
	text: true,
	media: true,
	containers: true,
	embeds: true,
})

function toggleCategory(id: string) {
	expanded.value[id] = !expanded.value[id]
}

const instance = useInstanceStore()
const allBlocks = computed(() => getRegisteredBlockTypes().filter((bt) => instance.isOffered(bt.name)))

function filteredBlocks(categoryId: BlockCategory): BlockType[] {
	return allBlocks.value.filter((block) => {
		const matchesCategory = block.category === categoryId
		const matchesSearch =
			!searchQuery.value ||
			block.title.toLowerCase().includes(searchQuery.value.toLowerCase()) ||
			block.description?.toLowerCase().includes(searchQuery.value.toLowerCase())
		const isTopLevel = !block.parent?.length
		return matchesCategory && matchesSearch && isTopLevel
	})
}

async function insertBlock(name: string) {
	const selectedId = store.selectedBlockId
	let parentId: string | undefined
	let insertIndex: number | undefined

	// Insert after selection, climbing out of parents whose allowedBlocks reject this type
	// (e.g. Heading while a Link inside a Card is selected).
	let ctx = selectedId ? store.getBlockContext(selectedId) : null
	while (ctx?.parentId) {
		const parent = store.findBlockById(ctx.parentId)
		const allowed = parent ? getBlockType(parent.name)?.allowedBlocks : undefined
		if (!allowed?.length || allowed.includes(name)) break
		ctx = store.getBlockContext(ctx.parentId)
	}
	if (ctx) {
		parentId = ctx.parentId ?? undefined
		insertIndex = ctx.index + 1
	}

	const block = createBlockInstance(name)
	store.addBlock(block, parentId, insertIndex)
	store.selectBlock(block.id)
	store.closeInserterOnMobile()
	await scrollBlockIntoView(block.id)
}

function onDragStart(e: DragEvent, name: string) {
	e.dataTransfer!.effectAllowed = 'copy'
	// Firefox won't start a drag without data; targets read the type from useBlockDrag
	e.dataTransfer!.setData('text/plain', name)
	startDrag({ kind: 'new', name })
}
</script>

<template>
	<div class="flex h-full flex-col">
		<div class="border-b border-gray-200 p-16">
			<input
				v-model="searchQuery"
				type="text"
				placeholder="Search blocks..."
				class="w-full rounded border border-gray-300 px-12 py-8 text-sm focus:border-primary focus:outline-hidden focus:ring-1 focus:ring-primary"
			/>
		</div>

		<div class="scrollbar-thin scrollbar-stable flex-1 overflow-y-auto p-16">
			<div
				v-for="(category, index) in categories"
				:key="index"
				class="mb-16"
			>
				<button
					class="flex w-full cursor-pointer items-center justify-between border-none bg-transparent p-0 text-left"
					@click="toggleCategory(category)"
				>
					<span class="text-xs font-semibold uppercase tracking-wide text-gray-500">
						{{ category }}
					</span>
					<span class="text-gray-400">{{ expanded[category] ? '−' : '+' }}</span>
				</button>

				<div
					v-show="expanded[category]"
					class="mt-8 space-y-4"
				>
					<button
						v-for="block in filteredBlocks(category)"
						:key="block.name"
						class="flex w-full cursor-grab items-center gap-12 rounded-md border-none bg-transparent px-12 py-8 text-left hover:bg-gray-100 active:cursor-grabbing"
						draggable="true"
						:title="`Click or drag to add ${block.title}`"
						data-testid="inserter-block"
						@click="insertBlock(block.name)"
						@dragstart="onDragStart($event, block.name)"
						@dragend="endDrag"
					>
						<span class="flex size-24 items-center justify-center text-gray-500">
							<component
								:is="block.icon"
								class="h-full w-full"
							/>
						</span>
						<div class="min-w-0 flex-1">
							<div class="text-sm font-medium text-gray-700">{{ block.title }}</div>
							<div
								v-if="block.description"
								class="truncate text-xs text-gray-400"
							>
								{{ block.description }}
							</div>
						</div>
					</button>
					<p
						v-if="filteredBlocks(category).length === 0"
						class="px-12 py-8 text-xs text-gray-400"
					>
						No blocks found
					</p>
				</div>
			</div>
		</div>
	</div>
</template>
