<script setup lang="ts">
import { computed } from 'vue'
import InlineToolbar from '../../editor/InlineToolbar.vue'
import type { BlockInstance } from '../../core/types'
import { useSelection } from '../../composables/useSelection'
import { useBlockAttribute, useBlockText } from '../../composables/useBlockAttribute'

const props = defineProps<{ block: BlockInstance }>()
const { isSelected } = useSelection()

const selected = computed(() => isSelected(props.block.id))

const { value: label, original: originalLabel } = useBlockText(() => props.block, 'label')
const url = useBlockAttribute(() => props.block, 'url', '')
const colorClass = computed(() => (props.block.attributes.textColor ? '' : 'text-primary'))
</script>

<template>
	<div class="p-16">
		<InlineToolbar
			:show="selected"
			:block-id="block.id"
		>
			<span class="mr-4 text-sm text-gray-400">{{ $t('editor.link.url') }}:</span>
			<input
				v-model="url"
				type="url"
				class="toolbar-input w-160"
				placeholder="https://..."
			/>
		</InlineToolbar>

		<input
			v-model="label"
			type="text"
			class="border-none bg-transparent p-0 outline-hidden placeholder:italic placeholder:text-gray-400 focus:ring-0"
			:class="colorClass"
			:placeholder="originalLabel || $t('editor.link.placeholder')"
		/>
	</div>
</template>
