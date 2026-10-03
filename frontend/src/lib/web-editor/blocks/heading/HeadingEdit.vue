<script setup lang="ts">
import { computed } from 'vue'
import InlineToolbar from '../../editor/InlineToolbar.vue'
import ToolbarDropdown from '../../editor/ToolbarDropdown.vue'
import type { BlockInstance } from '../../core/types'
import { useEditorStore } from '../../../../stores/editor'
import { useSelection } from '../../composables/useSelection'

const props = defineProps<{ block: BlockInstance }>()
const store = useEditorStore()
const { isSelected } = useSelection()

const selected = computed(() => isSelected(props.block.id))

const text = computed({
	get: () => props.block.attributes.text as string,
	set: (value: string) => store.updateBlockAttributes(props.block.id, { text: value }),
})

const level = computed({
	get: () => (props.block.attributes.level as number) || 2,
	set: (value: number) => store.updateBlockAttributes(props.block.id, { level: value }),
})

const levelOptions = [1, 2, 3, 4, 5, 6].map((n) => ({ value: n, label: `Heading ${n}` }))

const headingSizes: Record<number, string> = {
	1: 'text-4xl',
	2: 'text-3xl',
	3: 'text-2xl',
	4: 'text-xl',
	5: 'text-lg',
	6: 'text-base',
}
const colorClass = computed(() => (props.block.attributes.textColor ? '' : 'text-gray-900'))
</script>

<template>
	<div class="p-16">
		<InlineToolbar
			:show="selected"
			:block-id="block.id"
		>
			<ToolbarDropdown
				v-model="level"
				:options="levelOptions"
				title="Heading level"
			>
				<template #default="{ value }">H{{ value }}</template>
			</ToolbarDropdown>
		</InlineToolbar>

		<input
			v-model="text"
			type="text"
			class="m-0 w-full border-none bg-transparent p-0 font-bold outline-hidden placeholder:font-normal placeholder:italic placeholder:text-gray-400 focus:ring-0"
			:class="[headingSizes[level], colorClass]"
			placeholder="Click to add heading..."
		/>
	</div>
</template>
