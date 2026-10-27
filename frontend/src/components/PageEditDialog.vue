<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Alert, Button, FormField, Input, Modal, Switch, TagInput } from '@trainpaths/nb-ui'
import { useI18n } from 'vue-i18n'
import { putApiPagesById } from '../api/sdk.gen'
import type { UpdatePageRequest } from '../api/types.gen'
import { TAG_MAX_LENGTH, TAGS_PER_PAGE, errorMessage, normalizeTag, type PageSummary } from '../lib/web-editor'
import { usePagesStore } from '../stores/pages'
import { useTagsStore } from '../stores/tags'

/**
 * Pages-list modal: slug, publish status and tags of one page. Save sends only what changed, in one PUT (all or
 * nothing).
 */
const props = defineProps<{ page: PageSummary }>()
const emit = defineEmits<{ close: [] }>()

const pages = usePagesStore()
const { t } = useI18n()
const tagsStore = useTagsStore()
const slug = ref(props.page.slug)
const published = ref(props.page.status === 'published')
const tags = ref([...props.page.tags])
const saving = ref(false)
const error = ref<string | null>(null)

// same rule as the API's PageService.ValidateSlug (reserved slugs are left to the API)
const normalizedSlug = computed(() => slug.value.trim().toLowerCase())
const slugError = computed(() =>
	/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(normalizedSlug.value) && normalizedSlug.value.length <= 100
		? null
		: t('admin.pages.slugRule'),
)
const changed = computed(
	() =>
		normalizedSlug.value !== props.page.slug ||
		published.value !== (props.page.status === 'published') ||
		JSON.stringify(tags.value) !== JSON.stringify(props.page.tags),
)

onMounted(() => tagsStore.load())

async function save() {
	if (slugError.value || saving.value) return
	if (!changed.value) return emit('close')
	saving.value = true
	error.value = null
	const id = props.page.id
	const tagsChanged = JSON.stringify(tags.value) !== JSON.stringify(props.page.tags)
	const body: UpdatePageRequest = {}
	if (normalizedSlug.value !== props.page.slug) body.slug = normalizedSlug.value
	if (published.value !== (props.page.status === 'published')) body.status = published.value ? 'published' : 'draft'
	if (tagsChanged) body.tags = tags.value
	try {
		pages.applySaved((await putApiPagesById({ path: { id }, body })).data)
		if (tagsChanged) tagsStore.refresh()
		emit('close')
	} catch (err: unknown) {
		error.value = errorMessage(err, t('admin.pages.saveFailed'))
	} finally {
		saving.value = false
	}
}
</script>

<template>
	<!-- mounted with v-if by the list: always open; submit button sits in the footer, outside the form -->
	<Modal
		:open="true"
		:title="page.title"
		:persistent="saving"
		data-testid="page-edit-dialog"
		@close="emit('close')"
	>
		<form
			id="page-edit-form"
			class="flex flex-col gap-16"
			@submit.prevent="save"
		>
			<FormField
				:label="$t('admin.pages.slug')"
				:error="slugError ?? undefined"
				:hint="page.locked ? $t('admin.pages.lockedSlug') : undefined"
			>
				<Input
					v-model="slug"
					maxlength="100"
					autofocus
					:disabled="page.locked"
					data-testid="page-edit-slug"
				>
					<template #prefix>/</template>
				</Input>
			</FormField>
			<Switch
				v-model="published"
				:label="$t('admin.status.published')"
				:description="published ? $t('admin.pages.liveAt', { slug: normalizedSlug }) : $t('admin.pages.draftHint')"
				data-testid="page-edit-published"
			/>
			<!-- not FormField: TagInput doesn't take its label id -->
			<div class="flex flex-col gap-4">
				<span class="text-sm font-medium text-black">{{ $t('admin.pages.tags') }}</span>
				<TagInput
					v-model="tags"
					:suggestions="tagsStore.names"
					:popular="tagsStore.popular"
					:max="TAGS_PER_PAGE"
					:max-length="TAG_MAX_LENGTH"
					:normalize="normalizeTag"
					:label="$t('admin.pages.tags')"
				/>
			</div>
			<Alert
				v-if="error"
				type="error"
			>
				{{ error }}
			</Alert>
		</form>
		<template #footer>
			<Button
				variant="outline"
				:disabled="saving"
				@click="emit('close')"
			>
				{{ $t('common.cancel') }}
			</Button>
			<Button
				type="submit"
				form="page-edit-form"
				:loading="saving"
				:disabled="!!slugError"
				data-testid="page-edit-save"
			>
				{{ $t('common.save') }}
			</Button>
		</template>
	</Modal>
</template>
