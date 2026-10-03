<script setup lang="ts">
import { ref, computed } from 'vue'
import InlineToolbar from '../../editor/InlineToolbar.vue'
import ToolbarDropdown from '../../editor/ToolbarDropdown.vue'
import AlignIcon from './AlignIcon.vue'
import type { BlockInstance } from '../../core/types'
import { useSelection } from '../../composables/useSelection'
import { useBlockAttribute } from '../../composables/useBlockAttribute'
import { useAutoResize } from '../../composables/useAutoResize'
import { alignOptions, type Align } from './align'

const props = defineProps<{ block: BlockInstance }>()
const { isSelected } = useSelection()

const selected = computed(() => isSelected(props.block.id))

const text = useBlockAttribute(() => props.block, 'text', '')
const alignment = useBlockAttribute(() => props.block, 'alignment', 'left')
const alignClasses: Record<string, string> = { left: 'text-left', center: 'text-center', right: 'text-right' }
// default colour only when none chosen, else inherit from wrapper's inline colour
const colorClass = computed(() => (props.block.attributes.textColor ? '' : 'text-gray-700'))

const textarea = ref<HTMLTextAreaElement | null>(null)
useAutoResize(textarea, text)
</script>

<template>
	<div class="p-16">
		<InlineToolbar
			:show="selected"
			:block-id="block.id"
		>
			<ToolbarDropdown
				v-model="alignment"
				:options="alignOptions"
				title="Alignment"
				show-labels
			>
				<template #default="{ value }">
					<AlignIcon :align="value as Align" />
				</template>
			</ToolbarDropdown>
		</InlineToolbar>

		<textarea
			ref="textarea"
			v-model="text"
			rows="1"
			:class="[alignClasses[alignment], colorClass]"
			class="m-0 block w-full resize-none overflow-hidden border-none bg-transparent p-0 leading-relaxed outline-hidden placeholder:italic placeholder:text-gray-400 focus:ring-0"
			placeholder="Click to add text..."
		/>
	</div>
</template>
