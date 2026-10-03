<script setup lang="ts">
import { watch, onMounted, onUnmounted } from 'vue'
import BlockList from './BlockList.vue'
import EditorHeader from './EditorHeader.vue'
import EditorLayout from './EditorLayout.vue'
import { useEditorStore } from '../../../stores/editor'
import { useKeyboardNav } from '../composables/useKeyboardNav'
import { Loading } from '@trainpaths/nb-ui'
import { registerAllBlocks } from '../core/blockTypes'

const props = defineProps<{
	pageId: string
	/** Public URL prefix for the "View" link, e.g. '' → `/{slug}`. */
	publicBase?: string
	/** Where the draft preview lives, e.g. `/admin/pages/{id}/preview`. */
	previewPath?: string
}>()

const emit = defineEmits<{
	back: []
}>()

// editor-only (public views use core/blockViews.ts); idempotent
registerAllBlocks()

const store = useEditorStore()

useKeyboardNav()

onMounted(() => store.loadPage(props.pageId))
watch(
	() => props.pageId,
	(id) => store.loadPage(id),
)

async function handleBack() {
	if (store.changeCount > 0) await store.savePage()
	store.closePage()
	emit('back')
}

onUnmounted(() => {
	// Leaving via the router (not the back button): flush pending edits.
	if (store.pageId && store.changeCount > 0) void store.savePage()
})

let savedTimer: ReturnType<typeof setTimeout> | undefined
watch(
	() => store.saveStatus,
	(status) => {
		if (status === 'saved') {
			clearTimeout(savedTimer)
			savedTimer = setTimeout(() => {
				if (store.saveStatus === 'saved') store.saveStatus = 'idle'
			}, 2000)
		}
	},
)
</script>

<template>
	<div
		v-if="store.errorMessage"
		class="fixed left-1/2 top-16 z-50 -translate-x-1/2 transform"
	>
		<div
			class="flex items-center gap-12 rounded-md border border-red-200 bg-red-50 px-16 py-8 text-sm text-red-700 shadow-lg"
		>
			<span>{{ store.errorMessage }}</span>
			<button
				class="cursor-pointer border-none bg-transparent text-red-400 hover:text-red-600"
				@click="store.dismissError()"
			>
				&#10005;
			</button>
		</div>
	</div>

	<Loading
		v-if="store.loading && !store.pageId"
		label="Loading page…"
		fullscreen
	/>

	<div
		v-else-if="store.pageId"
		class="flex h-screen flex-col font-sans"
	>
		<EditorHeader
			:public-base="publicBase ?? ''"
			:preview-path="previewPath"
			@back="handleBack"
		/>
		<EditorLayout>
			<BlockList
				v-if="store.pageEditable"
				:blocks="store.blocks"
			/>
			<!-- template-only page (instance config "editable": false): nothing to edit on the canvas -->
			<div
				v-else
				class="font-cms m-auto max-w-400 rounded-lg border border-dashed border-gray-300 p-24 text-center text-sm text-gray-500"
				data-testid="template-page-notice"
			>
				The content of this page comes from the site's template
				<code class="text-gray-700">{{ store.pageTemplate }}</code>. You can still change its title and settings.
			</div>
		</EditorLayout>
	</div>
</template>
