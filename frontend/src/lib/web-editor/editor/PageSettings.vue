<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import SettingsSection from './SettingsSection.vue'
import { TagInput } from '@trainpaths/nb-ui'
import { META_DESCRIPTION_MAX, META_TITLE_MAX, TAG_MAX_LENGTH, TAGS_PER_PAGE, normalizeTag } from '../limits'
import { documentTitle } from '../../../public/head'
import { useEditorStore } from '../../../stores/editor'
import { useSiteConfigStore } from '../../../stores/siteConfig'
import { useTagsStore } from '../../../stores/tags'

const store = useEditorStore()
const tags = useTagsStore()
const siteConfig = useSiteConfigStore()
onMounted(() => {
	tags.load()
	siteConfig.load()
})

// what the public page's browser tab will show (firm name from the site config)
const tabTitle = computed(() =>
	documentTitle({ title: store.pageTitle, metaTitle: store.pageMetaTitle }, siteConfig.config),
)

const slugDraft = ref(store.pageSlug)
watch(
	() => store.pageSlug,
	(slug) => (slugDraft.value = slug),
)

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
		<SettingsSection title="Page">
			<!-- < sm only: the header hides its title input there -->
			<label class="field-label mb-12 sm:hidden">
				Title
				<input
					v-model="store.pageTitle"
					data-testid="page-title-mobile"
					class="input-field mt-4"
					@blur="store.pageTitle.trim() && store.savePage()"
					@keydown.enter="($event.target as HTMLInputElement).blur()"
				/>
			</label>
			<label class="field-label">
				Slug
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
				<template v-if="store.pageLocked">Fixed by the site configuration.</template>
				<template v-else>Lowercase letters, digits and hyphens.</template>
				Published pages are served at
				<span class="font-mono">/{{ store.pageSlug }}</span
				>.
			</p>
		</SettingsSection>
		<SettingsSection title="Search & sharing">
			<label class="field-label">
				Meta title
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
					>Tab: {{ tabTitle }}</span
				>
				<span class="shrink-0">{{ store.pageMetaTitle.length }}/{{ META_TITLE_MAX }}</span>
			</p>
			<label class="field-label">
				Meta description
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
				<span>Search results and link previews; empty = the first paragraph.</span>
				<span class="shrink-0">{{ store.pageMetaDescription.length }}/{{ META_DESCRIPTION_MAX }}</span>
			</p>
		</SettingsSection>
		<SettingsSection title="Tags">
			<TagInput
				:model-value="store.pageTags"
				:suggestions="tags.names"
				:popular="tags.popular"
				:max="TAGS_PER_PAGE"
				:max-length="TAG_MAX_LENGTH"
				:normalize="normalizeTag"
				test-id="page-tags"
				@update:model-value="store.updateTags"
			/>
		</SettingsSection>
		<SettingsSection title="Status">
			<div class="flex items-center justify-between text-sm">
				<span :class="store.pageStatus === 'published' ? 'text-green-700' : 'text-gray-600'">
					{{ store.pageStatus === 'published' ? 'Published' : 'Draft' }}
				</span>
				<button
					class="cursor-pointer rounded border-none bg-primary px-12 py-4 text-xs text-white hover:bg-primary-dark"
					@click="store.setPublished(store.pageStatus !== 'published')"
				>
					{{ store.pageStatus === 'published' ? 'Unpublish' : 'Publish' }}
				</button>
			</div>
		</SettingsSection>
	</div>
</template>
