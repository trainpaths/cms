<script setup lang="ts">
import { ref, computed } from 'vue'
import type { BlockInstance } from '../core/types'
import { getBlockType } from '../core/blockRegistry'
import { useEditorStore } from '../../../stores/editor'
import { useSelection } from '../composables/useSelection'
import { useToolbarPosition } from '../composables/useToolbarPosition'
import { useBlockDrag } from '../composables/useBlockDrag'
import { useIsMobile } from '../composables/useIsMobile'

const props = defineProps<{ block: BlockInstance }>()
const store = useEditorStore()
const { select, isSelected } = useSelection()
const { startDrag, endDrag } = useBlockDrag()

const blockType = computed(() => getBlockType(props.block.name))
const selected = computed(() => isSelected(props.block.id))
const isHovered = ref(false)

const wrapperRef = ref<HTMLElement | null>(null)
const toolbarRef = ref<HTMLElement | null>(null)
const isMobile = useIsMobile()
// mobile: toolbar lives in EditorLayout's top bar (only while selected), not floating by the block
const toolbarInBar = computed(() => isMobile.value && selected.value)
const toolbarVisible = computed(() => !isMobile.value && (selected.value || isHovered.value))
const { positionClass } = useToolbarPosition(wrapperRef, toolbarRef, toolbarVisible)

function onMouseEnter(e: MouseEvent) {
	e.stopPropagation()
	isHovered.value = true
}

function onMouseLeave(e: MouseEvent) {
	const related = e.relatedTarget as Node | null
	const wrapper = e.currentTarget as HTMLElement
	if (!related || !wrapper.contains(related)) {
		isHovered.value = false
	}
}

const context = computed(() => store.getBlockContext(props.block.id))
const isFirst = computed(() => context.value?.index === 0)
const isLast = computed(() => !!(context.value && context.value.index === context.value.siblings.length - 1))

function hasDescendant(block: BlockInstance, targetId: string): boolean {
	for (const child of block.innerBlocks) {
		if (child.id === targetId || hasDescendant(child, targetId)) return true
	}
	return false
}

const hasSelectedDescendant = computed(() => {
	if (!store.selectedBlockId) return false
	return hasDescendant(props.block, store.selectedBlockId)
})

const toolbarClass = computed(() => {
	if (toolbarInBar.value) return 'w-full rounded-none border-b border-gray-700'
	const base = `absolute right-0 rounded-md shadow-lg ${positionClass.value}`
	if (isMobile.value) return `${base} hidden`
	if (selected.value) return `${base} opacity-100`
	if (isHovered.value && !hasSelectedDescendant.value) return `${base} opacity-50`
	return `${base} opacity-0 pointer-events-none`
})

// ring = box-shadow: chrome without layout; the wrapper adds no spacing, blocks pad themselves
const borderClass = computed(() => {
	if (selected.value) return 'ring-2 ring-cms-primary shadow-md'
	if (isHovered.value && !hasSelectedDescendant.value) return 'ring-1 ring-gray-300'
	return ''
})

// overhang only when the canvas (EditorLayout `main`) has room for it: column max 768 + 2×8 (wide) / 2×24 (full);
// narrower canvas → normal width, no horizontal scroll
const widthClass = computed(() => {
	const width = props.block.attributes.blockWidth as string
	switch (width) {
		case 'wide':
			return '@min-[784px]/canvas:-ml-32 @min-[784px]/canvas:w-[calc(100%+64px)]'
		case 'full':
			return '@min-[816px]/canvas:-ml-48 @min-[816px]/canvas:w-[calc(100%+96px)]'
		default:
			return ''
	}
})

const blockStyles = computed(() => {
	const styles: Record<string, string> = {}
	const bg = props.block.attributes.backgroundColor as string
	const text = props.block.attributes.textColor as string
	if (bg) styles.backgroundColor = bg
	if (text) styles.color = text
	return styles
})

// drag only via handle: whole-block draggable hijacks text selection in inputs
function onDragStart(e: DragEvent) {
	e.dataTransfer!.effectAllowed = 'move'
	e.dataTransfer!.setData('text/plain', props.block.id)
	if (wrapperRef.value) e.dataTransfer!.setDragImage(wrapperRef.value, 16, 16)
	startDrag({ kind: 'move', id: props.block.id, name: props.block.name })
}
</script>

<template>
	<div
		v-if="blockType"
		ref="wrapperRef"
		:data-block-id="block.id"
		class="relative cursor-pointer transition-all duration-150"
		:class="[widthClass, selected ? 'z-10' : '']"
		@click.stop="select(block.id)"
		@mouseenter="onMouseEnter"
		@mouseleave="onMouseLeave"
	>
		<Teleport
			defer
			to="#editor-mobile-toolbar"
			:disabled="!toolbarInBar"
		>
			<div
				ref="toolbarRef"
				class="font-cms z-20 flex flex-wrap items-center gap-4 bg-gray-900 p-8 text-white transition-opacity"
				:class="toolbarClass"
			>
				<span
					class="drag-handle"
					title="Drag to move"
					draggable="true"
					data-testid="block-drag-handle"
					@dragstart.stop="onDragStart"
					@dragend="endDrag"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						width="16"
						height="16"
						viewBox="0 0 24 24"
						fill="currentColor"
					>
						<circle
							cx="9"
							cy="5"
							r="1.5"
						/>
						<circle
							cx="15"
							cy="5"
							r="1.5"
						/>
						<circle
							cx="9"
							cy="12"
							r="1.5"
						/>
						<circle
							cx="15"
							cy="12"
							r="1.5"
						/>
						<circle
							cx="9"
							cy="19"
							r="1.5"
						/>
						<circle
							cx="15"
							cy="19"
							r="1.5"
						/>
					</svg>
				</span>
				<span class="size-20 text-gray-300">
					<component
						:is="blockType.icon"
						class="h-full w-full"
					/>
				</span>
				<!-- narrow canvas (column < 320px): icon only, keeps the toolbar on one row -->
				<span class="text-md mr-8 text-gray-300 @max-xs:hidden">{{ blockType.title }}</span>
				<button
					class="floating-toolbar-btn"
					title="Move up"
					:disabled="isFirst"
					@click.stop="store.moveBlock(block.id, 'up')"
				>
					&#8593;
				</button>
				<button
					class="floating-toolbar-btn"
					title="Move down"
					:disabled="isLast"
					@click.stop="store.moveBlock(block.id, 'down')"
				>
					&#8595;
				</button>
				<button
					class="floating-toolbar-btn"
					title="Duplicate"
					@click.stop="store.duplicateBlock(block.id)"
				>
					&#10697;
				</button>
				<button
					class="floating-toolbar-btn text-red-400 hover:text-red-300"
					title="Remove"
					@click.stop="store.removeBlock(block.id)"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						width="18"
						height="18"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						stroke-linecap="round"
						stroke-linejoin="round"
					>
						<polyline points="3 6 5 6 21 6" />
						<path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
					</svg>
				</button>
				<span
					:id="`inline-toolbar-${block.id}`"
					class="contents"
				></span>
			</div>
		</Teleport>

		<div
			class="rounded-lg transition-all duration-150"
			:class="borderClass"
			:style="blockStyles"
		>
			<component
				:is="blockType.edit"
				:block="block"
			/>
		</div>
	</div>
</template>

<style>
@reference "../../../style.css";

.drag-handle {
	@apply flex cursor-grab items-center rounded px-2 text-gray-400 hover:bg-gray-700 hover:text-white active:cursor-grabbing;
}

.floating-toolbar-btn {
	@apply cursor-pointer rounded border-none bg-transparent px-5 text-gray-300 hover:bg-gray-700 hover:text-white disabled:cursor-not-allowed disabled:opacity-30;
}
</style>
