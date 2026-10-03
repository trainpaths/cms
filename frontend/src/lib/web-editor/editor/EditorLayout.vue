<script setup lang="ts">
import LeftSidebar from './LeftSidebar.vue'
import BlockSettingsPanel from './BlockSettingsPanel.vue'
import { useEditorStore } from '../../../stores/editor'
import { useSelection } from '../composables/useSelection'

const store = useEditorStore()
const { clear } = useSelection()

function handleMainClick(e: MouseEvent) {
	// root BlockList fills the canvas, so empty space isn't `main` itself any more
	if (!(e.target as HTMLElement).closest('[data-block-id]')) clear()
}
</script>

<template>
	<!-- md+: flex row, canvas takes what the open sidebars leave but never < 320px; then the sidebar slots shrink and
		their panels (absolute, w-280) overlap the canvas. Below md the sidebars are full-screen modals -->
	<div class="relative flex h-[calc(100vh-56px)]">
		<aside
			v-if="store.showInserterSidebar"
			class="fixed inset-x-0 top-56 bottom-0 z-30 md:relative md:inset-auto md:w-280 md:min-w-0"
		>
			<div
				class="h-full overflow-hidden bg-gray-50 md:absolute md:inset-y-0 md:left-0 md:w-280 md:border-r md:border-gray-200"
			>
				<LeftSidebar />
			</div>
		</aside>

		<!-- .site-theme: the instance's site theme applies to the canvas (WYSIWYG); chrome in it uses the cms-* tokens -->
		<main
			class="site-theme scrollbar-thin scrollbar-stable @container/canvas flex h-full min-w-320 flex-1 flex-col overflow-y-auto bg-white"
			@click="handleMainClick"
		>
			<!-- mobile: selected block's toolbar is teleported here (BlockWrapper); always rendered so the target exists -->
			<div
				v-show="store.selectedBlockId"
				id="editor-mobile-toolbar"
				class="sticky top-0 z-20 md:hidden"
				data-testid="mobile-block-toolbar"
				@click.stop
			></div>
			<!-- grow + flex: root BlockList fills the canvas, so any drop below the last block appends.
				@container: blocks size by this column (same as the public <article>), not the window -->
			<div class="@container mx-auto flex w-full max-w-3xl grow flex-col px-24 pt-48">
				<slot />
			</div>
		</main>

		<aside
			v-if="store.showSettingsPanel"
			class="fixed inset-x-0 top-56 bottom-0 z-30 md:relative md:inset-auto md:w-280 md:min-w-0"
		>
			<div
				class="h-full overflow-hidden bg-gray-50 md:absolute md:inset-y-0 md:right-0 md:w-280 md:border-l md:border-gray-200"
			>
				<BlockSettingsPanel />
			</div>
		</aside>
	</div>
</template>
