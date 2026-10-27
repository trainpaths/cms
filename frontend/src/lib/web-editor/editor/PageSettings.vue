<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { I18nT, useI18n } from 'vue-i18n'
import SettingsSection from './SettingsSection.vue'
import { TagInput } from '@trainpaths/nb-ui'
import { META_DESCRIPTION_MAX, META_TITLE_MAX, TAG_MAX_LENGTH, TAGS_PER_PAGE, normalizeTag } from '../limits'
import { documentTitle } from '../../../public/head'
import { useEditorStore } from '../../../stores/editor'
import { useSiteConfigStore } from '../../../stores/siteConfig'
import { useTagsStore } from '../../../stores/tags'
import { usePublishState } from '../composables/usePublishState'
import { pageExportFileName, toPageExport } from '../../pageExport'
import { saveJson } from '../../download'

const store = useEditorStore()
const { t } = useI18n()
const tags = useTagsStore()
const siteConfig = useSiteConfigStore()
const { published, canPublish, publishLabel, publishHint, statusText, publish, unpublish } = usePublishState()
onMounted(() => {
	tags.load()
	siteConfig.load()
})

// what the public page's browser tab will show (firm name from the site config)
const tabTitle = computed(() =>
	documentTitle(
		{ title: store.pageTitle, metaTitle: store.pageMetaTitle },
		siteConfig.config,
		t('site.notFound.title'),
	),
)

const slugDraft = ref(store.pageSlug)
watch(
	() => store.pageSlug,
	(slug) => (slugDraft.value = slug),
)

// what's on screen, saved or not
function downloadJson() {
	saveJson(
		toPageExport({
			title: store.pageTitle,
			slug: store.pageSlug,
			metaTitle: store.pageMetaTitle,
			metaDescription: store.pageMetaDescription,
			tags: store.pageTags,
			blocks: store.blocks,
		}),
		pageExportFileName(store.pageSlug),
	)
}

async function commitSlug() {
	const slug = slugDraft.value.trim().toLowerCase()
	if (!slug || slug === store.pageSlug) {
		slugDraft.value = store.pageSlug
		return
	}
	if (!(await store.updateSlug(slug))) slugDraft.value = store.pageSlug
}
</script>

<template>
	<div class="p-16">
		<SettingsSection :title="$t('editor.page.section')">
			<!-- < sm only: the header hides its title input there -->
			<label class="field-label mb-12 sm:hidden">
				{{ $t('editor.page.title') }}
				<input
					v-model="store.pageTitle"
					data-testid="page-title-mobile"
					class="input-field mt-4"
					@blur="store.pageTitle.trim() && store.savePage()"
					@keydown.enter="($event.target as HTMLInputElement).blur()"
				/>
			</label>
			<label class="field-label">
				{{ $t('admin.pages.slug') }}
				<div class="mt-4 flex items-center rounded border border-gray-300 bg-white text-sm">
					<span class="pl-8 text-gray-400">/</span>
					<input
						v-model="slugDraft"
						data-testid="page-slug"
						class="w-full border-none bg-transparent px-2 py-4 outline-hidden disabled:cursor-not-allowed disabled:text-gray-500"
						:disabled="store.pageLocked"
						@blur="commitSlug"
						@keydown.enter="($event.target as HTMLInputElement).blur()"
					/>
				</div>
			</label>
			<p class="mt-8 mb-0 text-xs text-gray-400">
				<template v-if="store.pageLocked">{{ $t('editor.page.slugLocked') }}</template>
				<template v-else>{{ $t('editor.page.slugRule') }}</template>
				{{ ' ' }}
				<I18nT
					keypath="editor.page.servedAt"
					scope="global"
				>
					<template #path>
						<span class="font-mono">/{{ store.pageSlug }}</span>
					</template>
				</I18nT>
			</p>
		</SettingsSection>
		<SettingsSection :title="$t('editor.page.seo')">
			<label class="field-label">
				{{ $t('editor.page.metaTitle') }}
				<input
					v-model="store.pageMetaTitle"
					:maxlength="META_TITLE_MAX"
					data-testid="page-meta-title"
					class="input-field mt-4"
					@blur="store.savePage()"
					@keydown.enter="($event.target as HTMLInputElement).blur()"
				/>
			</label>
			<p class="mt-4 mb-12 flex justify-between gap-8 text-xs text-gray-400">
				<span
					class="min-w-0 truncate"
					data-testid="page-tab-title"
					:title="tabTitle"
					>{{ $t('editor.page.tab', { title: tabTitle }) }}</span
				>
				<span class="shrink-0">{{ store.pageMetaTitle.length }}/{{ META_TITLE_MAX }}</span>
			</p>
			<label class="field-label">
				{{ $t('editor.page.metaDescription') }}
				<textarea
					v-model="store.pageMetaDescription"
					:maxlength="META_DESCRIPTION_MAX"
					rows="3"
					data-testid="page-meta-description"
					class="input-field mt-4 resize-y"
					@blur="store.savePage()"
				/>
			</label>
			<p class="mt-4 mb-0 flex justify-between gap-8 text-xs text-gray-400">
				<span>{{ $t('editor.page.metaDescriptionHint') }}</span>
				<span class="shrink-0">{{ store.pageMetaDescription.length }}/{{ META_DESCRIPTION_MAX }}</span>
			</p>
		</SettingsSection>
		<SettingsSection :title="$t('admin.pages.tags')">
			<TagInput
				:model-value="store.pageTags"
				:suggestions="tags.names"
				:popular="tags.popular"
				:max="TAGS_PER_PAGE"
				:max-length="TAG_MAX_LENGTH"
				:normalize="normalizeTag"
				test-id="page-tags"
				:label="$t('admin.pages.tags')"
				@update:model-value="store.updateTags"
			/>
		</SettingsSection>
		<SettingsSection :title="$t('editor.page.status')">
			<p
				class="mt-0 mb-8 text-sm"
				:class="published ? 'text-green-700' : 'text-gray-600'"
				data-testid="page-status"
			>
				{{ statusText }}
			</p>
			<div class="flex flex-wrap items-center gap-8">
				<button
					class="cursor-pointer rounded border-none bg-primary px-12 py-4 text-xs text-white hover:bg-primary-dark disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-primary"
					:title="publishHint"
					:disabled="!canPublish"
					data-testid="settings-publish"
					@click="publish"
				>
					{{ publishLabel }}
				</button>
				<button
					v-if="published"
					class="cursor-pointer rounded border border-gray-300 bg-white px-12 py-4 text-xs text-gray-700 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
					:disabled="store.publishing"
					data-testid="settings-unpublish"
					@click="unpublish"
				>
					{{ $t('editor.publish.unpublish') }}
				</button>
			</div>
			<p class="mt-8 mb-0 text-xs text-gray-400">{{ $t('editor.page.statusHint') }}</p>
		</SettingsSection>
		<SettingsSection :title="$t('admin.pages.export')">
			<button
				class="cursor-pointer rounded border border-gray-300 bg-white px-12 py-4 text-xs text-gray-700 hover:bg-gray-50"
				data-testid="page-download-json"
				@click="downloadJson"
			>
				{{ $t('editor.page.downloadJson') }}
			</button>
			<p class="mt-8 mb-0 text-xs text-gray-400">
				{{ $t('editor.page.exportHint') }}
			</p>
		</SettingsSection>
	</div>
</template>
