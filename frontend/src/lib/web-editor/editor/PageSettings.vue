<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { I18nT, useI18n } from 'vue-i18n'
import SettingsSection from './SettingsSection.vue'
import { Badge, Button, TagInput } from '@trainpaths/nb-ui'
import { META_DESCRIPTION_MAX, META_TITLE_MAX, TAG_MAX_LENGTH, TAGS_PER_PAGE, normalizeTag } from '../limits'
import { documentTitle } from '../../../public/head'
import { useEditorStore } from '../../../stores/editor'
import { useSiteConfigStore } from '../../../stores/siteConfig'
import { useTagsStore } from '../../../stores/tags'
import { usePublishState } from '../composables/usePublishState'
import { pageExportFileName, toPageExport } from '../../pageExport'
import { saveJson } from '../../download'
import { usePageText } from '../composables/usePageText'
import { useMachineTranslation } from '../composables/useMachineTranslation'
import { DEFAULT_LANGUAGE, IS_LOCALIZED, LANGUAGES, languageLabel } from '../../../i18n/languages'
import { localePath } from '../../../public/locale'

const store = useEditorStore()
const { t } = useI18n()
const tags = useTagsStore()
const siteConfig = useSiteConfigStore()
const { published, canPublish, publishLabel, publishHint, statusText, publish, unpublish } = usePublishState()
onMounted(() => {
	tags.load()
	siteConfig.load()
})

// in the editor's language (translations: the default's value as placeholder, used while empty)
const title = usePageText('title')
const metaTitle = usePageText('metaTitle')
const metaDescription = usePageText('metaDescription')
const deepl = useMachineTranslation()
const translationRows = computed(() =>
	LANGUAGES.filter(({ code }) => code !== DEFAULT_LANGUAGE.code).map(({ code }) => ({
		code,
		name: languageLabel(code),
		...(store.translationStatus[code] ?? { missing: 0, outdated: 0 }),
	})),
)
const editedStatus = computed(() => store.translationStatus[store.editLang])

// "2 untranslated · 1 outdated" / "Complete"
function rowStatus(row: { missing: number; outdated: number }): string {
	const parts = [
		...(row.missing ? [t('editor.translations.missing', row.missing)] : []),
		...(row.outdated ? [t('editor.translations.outdated', row.outdated)] : []),
	]
	return parts.length ? parts.join(' · ') : t('editor.translations.complete')
}

// what the public page's browser tab will show (firm name from the site config)
const tabTitle = computed(() =>
	documentTitle(
		{
			title: title.value.value || title.original.value,
			metaTitle: metaTitle.value.value || metaTitle.original.value,
		},
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
			translations: store.pageTranslations,
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
					v-model="title.value.value"
					:placeholder="title.original.value"
					:class="{ 'border-amber-500!': title.outdated.value }"
					:title="title.outdated.value ? $t('editor.translations.fieldOutdated') : undefined"
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
						<span class="font-mono">{{ localePath(`/${store.pageSlug}`, store.editLang) }}</span>
					</template>
				</I18nT>
			</p>
		</SettingsSection>
		<SettingsSection :title="$t('editor.page.seo')">
			<label class="field-label">
				{{ $t('editor.page.metaTitle') }}
				<input
					v-model="metaTitle.value.value"
					:placeholder="metaTitle.original.value"
					:class="{ 'border-amber-500!': metaTitle.outdated.value }"
					:title="metaTitle.outdated.value ? $t('editor.translations.fieldOutdated') : undefined"
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
				<span class="shrink-0">{{ metaTitle.value.value.length }}/{{ META_TITLE_MAX }}</span>
			</p>
			<label class="field-label">
				{{ $t('editor.page.metaDescription') }}
				<textarea
					v-model="metaDescription.value.value"
					:placeholder="metaDescription.original.value"
					:class="{ 'border-amber-500!': metaDescription.outdated.value }"
					:title="metaDescription.outdated.value ? $t('editor.translations.fieldOutdated') : undefined"
					:maxlength="META_DESCRIPTION_MAX"
					rows="3"
					data-testid="page-meta-description"
					class="input-field mt-4 resize-y"
					@blur="store.savePage()"
				/>
			</label>
			<p class="mt-4 mb-0 flex justify-between gap-8 text-xs text-gray-400">
				<span>{{ $t('editor.page.metaDescriptionHint') }}</span>
				<span class="shrink-0">{{ metaDescription.value.value.length }}/{{ META_DESCRIPTION_MAX }}</span>
			</p>
		</SettingsSection>
		<SettingsSection
			v-if="IS_LOCALIZED"
			:title="$t('editor.translations.section')"
		>
			<ul class="m-0 flex list-none flex-col gap-4 p-0">
				<li
					v-for="row in translationRows"
					:key="row.code"
				>
					<button
						class="flex w-full cursor-pointer items-center justify-between gap-8 rounded border px-8 py-4 text-left text-sm"
						:class="
							store.editLang === row.code
								? 'border-primary bg-primary/10 text-primary'
								: 'border-gray-200 bg-white text-gray-700 hover:bg-gray-50'
						"
						:aria-pressed="store.editLang === row.code"
						:data-testid="`translation-${row.code}`"
						@click="store.editLang = row.code"
					>
						<span>{{ row.name }}</span>
						<Badge
							size="sm"
							:color="row.missing || row.outdated ? 'warning' : 'success'"
						>
							{{ rowStatus(row) }}
						</Badge>
					</button>
				</li>
			</ul>
			<div
				v-if="deepl.available.value"
				class="mt-8 flex flex-wrap items-center gap-8"
			>
				<Button
					bg="primary"
					size="sm"
					:loading="deepl.running.value"
					data-testid="deepl-missing"
					@click="deepl.translateMissing()"
				>
					{{ editedStatus?.outdated ? $t('editor.deepl.missingAndOutdated') : $t('editor.deepl.missing') }}
				</Button>
				<Button
					variant="outline"
					text="primary"
					size="sm"
					:disabled="deepl.running.value"
					data-testid="deepl-all"
					@click="deepl.retranslateAll()"
				>
					{{ $t('editor.deepl.retranslate') }}
				</Button>
			</div>
			<Button
				v-if="store.editLang !== DEFAULT_LANGUAGE.code"
				variant="ghost"
				text="secondary"
				size="sm"
				class="mt-8"
				@click="store.editLang = DEFAULT_LANGUAGE.code"
			>
				{{ $t('editor.translations.backToDefault', { language: languageLabel(DEFAULT_LANGUAGE.code) }) }}
			</Button>
			<p class="mt-8 mb-0 text-xs text-gray-400">{{ $t('editor.translations.hint') }}</p>
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
				<Button
					bg="primary"
					size="sm"
					:title="publishHint"
					:disabled="!canPublish"
					data-testid="settings-publish"
					@click="publish"
				>
					{{ publishLabel }}
				</Button>
				<Button
					v-if="published"
					variant="outline"
					text="secondary"
					border="secondary"
					size="sm"
					:disabled="store.publishing"
					data-testid="settings-unpublish"
					@click="unpublish"
				>
					{{ $t('editor.publish.unpublish') }}
				</Button>
			</div>
			<p class="mt-8 mb-0 text-xs text-gray-400">{{ $t('editor.page.statusHint') }}</p>
		</SettingsSection>
		<SettingsSection :title="$t('admin.pages.export')">
			<Button
				variant="outline"
				text="primary"
				size="sm"
				data-testid="page-download-json"
				@click="downloadJson"
			>
				{{ $t('editor.page.downloadJson') }}
			</Button>
			<p class="mt-8 mb-0 text-xs text-gray-400">
				{{ $t('editor.page.exportHint') }}
			</p>
		</SettingsSection>
	</div>
</template>
