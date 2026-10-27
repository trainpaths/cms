<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { Button, Icon } from '@trainpaths/nb-ui'
import { useEditorStore } from '../../../stores/editor'
import { usePublishState } from '../composables/usePublishState'
import { usePageText } from '../composables/usePageText'
import EditLanguageSelect from './EditLanguageSelect.vue'
import { DEFAULT_LANGUAGE } from '../../../i18n/languages'
import { localePath } from '../../../public/locale'

const props = defineProps<{
	publicBase: string
	previewPath?: string
}>()

const emit = defineEmits<{
	back: []
}>()

const store = useEditorStore()
const { published: isPublished, canPublish, publishLabel, publishHint, publish, unpublish } = usePublishState()

// in the language being edited
const viewHref = computed(() => {
	const translating = store.editLang !== DEFAULT_LANGUAGE.code
	if (isPublished.value) return `${props.publicBase}${localePath(`/${store.pageSlug}`, store.editLang)}`
	if (!props.previewPath) return ''
	return translating ? `${props.previewPath}?lang=${store.editLang}` : props.previewPath
})

const { value: title, original: originalTitle, outdated: titleOutdated } = usePageText('title')

// the default title is required; a translation may be empty (= untranslated)
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
				:title="$t('common.back')"
				:aria-label="$t('common.back')"
				@click="emit('back')"
			>
				<Icon name="arrow" :size="18" />
			</button>
			<button
				class="header-icon-btn"
				:class="{ 'header-icon-btn--active': store.showInserterSidebar }"
				:title="$t('editor.header.leftSidebarHint')"
				:aria-label="$t('editor.header.leftSidebar')"
				:aria-pressed="store.showInserterSidebar"
				data-testid="toggle-left-sidebar"
				@click="store.toggleInserterSidebar()"
			>
				<Icon name="panel" :size="18" />
			</button>
			<!-- < sm: hidden, no room (title + publish live in the Page tab there) -->
			<input
				v-model="title"
				:placeholder="originalTitle"
				:title="titleOutdated ? $t('editor.translations.fieldOutdated') : undefined"
				:class="{ 'underline decoration-amber-500 decoration-wavy': titleOutdated }"
				:aria-label="$t('editor.page.title')"
				data-testid="page-title"
				class="m-0 min-w-0 flex-1 truncate border-none bg-transparent text-xl font-semibold text-gray-900 outline-hidden focus:ring-1 focus:ring-primary max-sm:hidden"
				@blur="handleTitleBlur"
			/>
		</div>
		<div class="flex shrink-0 items-center gap-4 md:gap-8">
			<EditLanguageSelect />
			<button
				class="toolbar-btn"
				:title="$t('editor.header.undo')"
				:disabled="!store.canUndo"
				@click="store.undo()"
			>
				&#8630;
			</button>
			<button
				class="toolbar-btn"
				:title="$t('editor.header.redo')"
				:disabled="!store.canRedo"
				@click="store.redo()"
			>
				&#8631;
			</button>
			<span
				v-if="store.saveStatus === 'saved'"
				class="hidden text-sm text-green-600 md:inline"
				>{{ $t('editor.header.saved') }}</span
			>
			<RouterLink
				v-if="viewHref"
				:to="viewHref"
				class="header-icon-btn no-underline"
				:title="isPublished ? $t('admin.pages.view') : $t('admin.pages.previewDraft')"
				:aria-label="isPublished ? $t('admin.pages.view') : $t('admin.pages.previewDraft')"
				target="_blank"
			>
				<Icon name="eye" :size="18" />
			</RouterLink>
			<Button
				bg="primary"
				square
				class="shrink-0"
				:loading="store.saveStatus === 'saving'"
				:title="
					store.saveStatus === 'saving'
						? $t('common.saving')
						: store.isDirty
							? $t('editor.header.saveHint')
							: $t('editor.header.noChanges')
				"
				:aria-label="$t('common.save')"
				data-testid="page-save"
				:disabled="!store.isDirty"
				@click="store.savePage()"
			>
				<Icon name="save" :size="18" />
			</Button>
			<!-- md+: text button; < md: globe icon (same rules) -->
			<Button
				variant="outline"
				text="primary"
				class="whitespace-nowrap max-md:hidden"
				:title="publishHint"
				:disabled="!canPublish"
				data-testid="page-publish"
				@click="publish"
			>
				{{ publishLabel }}
			</Button>
			<!-- wrappers: .header-icon-btn's display would beat a hidden utility on the button itself -->
			<span class="contents md:hidden">
				<button
					class="header-icon-btn disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-transparent"
					:title="publishHint"
					:aria-label="publishLabel"
					:disabled="!canPublish"
					data-testid="page-publish-icon"
					@click="publish"
				>
					<Icon
						name="globe"
						:size="18"
					/>
				</button>
			</span>
			<!-- < md: hidden, the Page tab has it -->
			<Button
				v-if="isPublished"
				variant="outline"
				text="secondary"
				border="secondary"
				class="whitespace-nowrap max-md:hidden"
				:disabled="store.publishing"
				data-testid="page-unpublish"
				@click="unpublish"
			>
				<Icon
					name="globe-slash"
					:size="16"
				/>
				{{ $t('editor.publish.unpublish') }}
			</Button>
			<button
				class="header-icon-btn"
				:class="{ 'header-icon-btn--active': store.showSettingsPanel }"
				:title="$t('editor.header.rightSidebar')"
				:aria-label="$t('editor.header.rightSidebar')"
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
