<script setup lang="ts">
import { ref, computed } from 'vue'
import BlockWrapper from './BlockWrapper.vue'
import Inserter from './Inserter.vue'
import type { BlockInstance } from '../core/types'
import { createBlockInstance } from '../core/blockRegistry'
import { useEditorStore } from '../../../stores/editor'
import { useBlockDrag } from '../composables/useBlockDrag'

const props = defineProps<{
	blocks: BlockInstance[]
	parentId?: string
}>()

const store = useEditorStore()
const { dragged, dropTarget, endDrag, canDropInto } = useBlockDrag()
const listEl = ref<HTMLElement | null>(null)
const isRoot = computed(() => !props.parentId)
const dropIndex = computed(() =>
	dropTarget.value && dropTarget.value.parentId === (props.parentId ?? null) ? dropTarget.value.index : null,
)

/** Upper half of a block → before it, lower half → after it; below the last midpoint → end. */
function indexAt(clientY: number) {
	const items = listEl.value?.querySelectorAll<HTMLElement>(':scope > [data-drop-item]') ?? []
	for (let i = 0; i < items.length; i++) {
		const rect = items[i].getBoundingClientRect()
		if (clientY < rect.top + rect.height / 2) return i
	}
	return props.blocks.length
}

// Every handler stops propagation: the innermost list decides. Invalid target → no preventDefault,
// so no indicator and the browser shows "no drop" instead of an outer list lighting up.
function onDragOver(e: DragEvent) {
	e.stopPropagation()
	const parentId = props.parentId ?? null
	if (!canDropInto(parentId)) {
		dropTarget.value = null
		return
	}
	e.preventDefault()
	e.dataTransfer!.dropEffect = dragged.value?.kind === 'new' ? 'copy' : 'move'
	const index = indexAt(e.clientY)
	if (dropTarget.value?.parentId !== parentId || dropTarget.value.index !== index) {
		dropTarget.value = { parentId, index }
	}
}

function onDragLeave(e: DragEvent) {
	// moving between children fires leave too; only clear when the pointer really left this list
	const related = e.relatedTarget as Node | null
	if (related && listEl.value?.contains(related)) return
	if (dropIndex.value !== null) dropTarget.value = null
}

function onDrop(e: DragEvent) {
	e.stopPropagation()
	const d = dragged.value
	const parentId = props.parentId ?? null
	if (!d || !canDropInto(parentId)) return
	e.preventDefault()
	const target = indexAt(e.clientY)
	endDrag()

	if (d.kind === 'new') {
		const block = createBlockInstance(d.name)
		store.addBlock(block, props.parentId, target)
		store.selectBlock(block.id)
		return
	}

	const sourceCtx = store.getBlockContext(d.id)
	if (!sourceCtx) return
	const adjusted = parentId === sourceCtx.parentId && sourceCtx.index < target ? target - 1 : target
	store.moveBlockToPosition(d.id, parentId, adjusted)
}
</script>

<template>
	<div
		ref="listEl"
		class="flex flex-col"
		:class="isRoot ? 'flex-1 pb-32' : ''"
		@dragover="onDragOver"
		@dragleave="onDragLeave"
		@drop="onDrop"
	>
		<div
			v-if="isRoot && !blocks.length"
			class="flex flex-col items-center gap-12 rounded-lg border-2 border-dashed py-48 text-center"
			:class="dropIndex === 0 ? 'border-cms-primary bg-cms-primary/5' : 'border-gray-200'"
			:data-testid="dropIndex === 0 ? 'drop-indicator' : undefined"
		>
			<span class="text-4xl text-gray-300">+</span>
			<p class="m-0 text-sm text-gray-400">Start adding blocks from the sidebar</p>
		</div>
		<template v-else>
			<!-- slots always take 10px (gap between blocks): active 4px line + 3px each side, grows from 0, no layout shift -->
			<template
				v-for="(block, i) in blocks"
				:key="block.id"
			>
				<div class="flex h-10 items-center">
					<div
						class="w-full transition-[height] duration-150 ease-out"
						:class="dropIndex === i ? 'h-4 bg-cms-primary' : 'h-0'"
						:data-testid="dropIndex === i ? 'drop-indicator' : undefined"
					/>
				</div>
				<div data-drop-item>
					<BlockWrapper :block="block" />
				</div>
			</template>
			<div class="flex h-10 items-center">
				<div
					class="w-full transition-[height] duration-150 ease-out"
					:class="dropIndex === blocks.length ? 'h-4 bg-cms-primary' : 'h-0'"
					:data-testid="dropIndex === blocks.length ? 'drop-indicator' : undefined"
				/>
			</div>
		</template>
		<Inserter
			v-if="parentId"
			:parent-id="parentId"
		/>
	</div>
</template>
