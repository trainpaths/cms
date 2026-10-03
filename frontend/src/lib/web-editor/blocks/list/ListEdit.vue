<script setup lang="ts">
import { computed } from 'vue'
import BlockList from '../../editor/BlockList.vue'
import InlineToolbar from '../../editor/InlineToolbar.vue'
import type { BlockInstance } from '../../core/types'
import { useSelection } from '../../composables/useSelection'
import { useBlockAttribute } from '../../composables/useBlockAttribute'

const props = defineProps<{ block: BlockInstance }>()
const { isSelected } = useSelection()

const selected = computed(() => isSelected(props.block.id))

const ordered = useBlockAttribute(() => props.block, 'ordered', false)
const listType = computed({
	get: () => (ordered.value ? 'numbered' : 'bullet'),
	set: (value: string) => (ordered.value = value === 'numbered'),
})
</script>

<template>
	<div class="p-16">
		<InlineToolbar
			:show="selected"
			:block-id="block.id"
		>
			<select
				v-model="listType"
				class="inline-toolbar-select"
			>
				<option value="bullet">Bullet</option>
				<option value="numbered">Numbered</option>
			</select>
		</InlineToolbar>

		<div class="inner-blocks-container m-0 pl-24">
			<BlockList
				:blocks="block.innerBlocks"
				:parent-id="block.id"
			/>
		</div>
	</div>
</template>

<style scoped>
@reference "../../../../style.css";

.inline-toolbar-select {
	@apply cursor-pointer rounded border-none bg-gray-700 px-8 py-4 text-sm text-white outline-hidden;
}
</style>
