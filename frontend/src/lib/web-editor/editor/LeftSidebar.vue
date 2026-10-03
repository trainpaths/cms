<script setup lang="ts">
import BlockInserterSidebar from './BlockInserterSidebar.vue'
import BlockOutline from './BlockOutline.vue'
import SidebarTabs from './SidebarTabs.vue'
import { useEditorStore } from '../../../stores/editor'

/** Left editor panel: "Blocks" (inserter) and "Outline" (the page's block tree) tabs. */
const store = useEditorStore()

const tabs = [
	{ id: 'blocks', label: 'Blocks' },
	{ id: 'outline', label: 'Outline' },
] as const
</script>

<template>
	<div class="flex h-full flex-col">
		<SidebarTabs
			v-model="store.leftSidebarTab"
			:tabs="tabs"
			close-testid="sidebar-close-left"
			@close="store.toggleInserterSidebar()"
		/>
		<div class="min-h-0 flex-1">
			<BlockInserterSidebar v-if="store.leftSidebarTab === 'blocks'" />
			<BlockOutline v-else />
		</div>
	</div>
</template>
