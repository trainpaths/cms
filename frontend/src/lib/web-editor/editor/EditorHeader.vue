<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { Icon } from '@trainpaths/nb-ui'
import { useEditorStore } from '../../../stores/editor'

const props = defineProps<{
	publicBase: string
	previewPath?: string
}>()

const emit = defineEmits<{
	back: []
}>()

const store = useEditorStore()

const isPublished = computed(() => store.pageStatus === 'published')
const viewHref = computed(() =>
	isPublished.value ? `${props.publicBase}/${store.pageSlug}` : (props.previewPath ?? ''),
)

function handleTitleBlur() {
	if (store.pageTitle.trim()) {
		store.savePage()
	}
}
</script>

<template>
	<header
		class="sticky top-0 z-10 flex h-56 items-center justify-between gap-8 border-b border-gray-200 bg-white px-8 md:gap-12 md:px-16"
	>
		<div class="flex min-w-0 flex-1 items-center gap-4 md:gap-8">
			<button
				class="header-icon-btn"
				title="Back"
				aria-label="Back"
				@click="emit('back')"
			>
				<Icon name="arrow" :size="18" />
			</button>
			<button
				class="header-icon-btn"
				:class="{ 'header-icon-btn--active': store.showInserterSidebar }"
				title="Toggle left sidebar (blocks, outline)"
				aria-label="Toggle left sidebar"
				:aria-pressed="store.showInserterSidebar"
				data-testid="toggle-left-sidebar"
				@click="store.toggleInserterSidebar()"
			>
				<Icon name="panel" :size="18" />
			</button>
			<!-- < sm: hidden, no room (title + publish live in the Page tab there) -->
			<input
				v-model="store.pageTitle"
				data-testid="page-title"
				class="m-0 min-w-0 flex-1 truncate border-none bg-transparent text-xl font-semibold text-gray-900 outline-hidden focus:ring-1 focus:ring-primary max-sm:hidden"
				@blur="handleTitleBlur"
			/>
		</div>
		<div class="flex shrink-0 items-center gap-4 md:gap-8">
			<button
				class="toolbar-btn"
				title="Undo (Ctrl+Z)"
				:disabled="!store.canUndo"
				@click="store.undo()"
			>
				&#8630;
			</button>
			<button
				class="toolbar-btn"
				title="Redo (Ctrl+Shift+Z)"
				:disabled="!store.canRedo"
				@click="store.redo()"
			>
				&#8631;
			</button>
			<span
				v-if="store.saveStatus === 'saved'"
				class="hidden text-sm text-green-600 md:inline"
				>Saved</span
			>
			<RouterLink
				v-if="viewHref"
				:to="viewHref"
				class="header-icon-btn no-underline"
				:title="isPublished ? 'View' : 'Preview'"
				:aria-label="isPublished ? 'View' : 'Preview'"
				target="_blank"
			>
				<Icon name="external-link" :size="18" />
			</RouterLink>
			<!-- < sm: hidden, Page tab has the publish toggle -->
			<button
				class="cursor-pointer rounded border border-gray-300 bg-white px-8 py-6 text-sm text-gray-700 hover:bg-gray-50 max-sm:hidden md:px-12"
				data-testid="publish-toggle"
				@click="store.setPublished(!isPublished)"
			>
				{{ isPublished ? 'Unpublish' : 'Publish' }}
			</button>
			<button
				class="flex size-32 shrink-0 cursor-pointer items-center justify-center rounded border-none bg-primary md:size-36 text-white hover:bg-primary-dark disabled:cursor-not-allowed disabled:opacity-60"
				:class="{ 'animate-pulse': store.saveStatus === 'saving' }"
				:title="store.saveStatus === 'saving' ? 'Saving...' : 'Save'"
				aria-label="Save"
				:disabled="store.saveStatus === 'saving'"
				@click="store.savePage()"
			>
				<Icon name="save" :size="18" />
			</button>
			<button
				class="header-icon-btn"
				:class="{ 'header-icon-btn--active': store.showSettingsPanel }"
				title="Toggle settings sidebar"
				aria-label="Toggle settings sidebar"
				:aria-pressed="store.showSettingsPanel"
				data-testid="toggle-right-sidebar"
				@click="store.toggleSettingsPanel()"
			>
				<Icon name="panel" :size="18" :rotate="180" />
			</button>
		</div>
	</header>
</template>

<style>
@reference "../../../style.css";

/* square icon button; --active = sidebar open */
.header-icon-btn {
	@apply flex size-32 shrink-0 md:size-36 cursor-pointer items-center justify-center rounded border border-transparent bg-transparent text-gray-600 hover:bg-gray-100 hover:text-gray-900;
}

.header-icon-btn--active {
	@apply bg-primary/10 text-primary hover:bg-primary/15 hover:text-primary;
}

.toolbar-btn {
	@apply cursor-pointer rounded border-none bg-transparent px-8 py-2 text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-30;
}
</style>
