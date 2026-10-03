<script setup lang="ts">
import { computed, ref } from 'vue'
import {
	InlineToolbar,
	ToolbarDropdown,
	useAutoResize,
	useBlockAttribute,
	useSelection,
	type BlockInstance,
} from '@trainpaths/cms/editor'
import { toneClass, toneOptions } from './tone'

const props = defineProps<{ block: BlockInstance }>()

const { isSelected } = useSelection()
const selected = computed(() => isSelected(props.block.id))
const text = useBlockAttribute(() => props.block, 'text', '')
const tone = useBlockAttribute(() => props.block, 'tone', 'info')
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
				v-model="tone"
				:options="toneOptions"
				title="Tone"
			/>
		</InlineToolbar>
		<div
			class="rounded-md border-l-4 px-16 py-12"
			:class="toneClass(tone)"
		>
			<textarea
				ref="textarea"
				v-model="text"
				rows="1"
				placeholder="Write a note..."
				class="w-full resize-none border-none bg-transparent font-sans text-base text-gray-800 outline-hidden"
				data-testid="callout-input"
			/>
		</div>
	</div>
</template>
