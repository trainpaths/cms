<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Alert, Button, FormField, Input, Modal, Switch, TagInput } from '@trainpaths/nb-ui'
import {
	postApiPagesByIdPublish,
	postApiPagesByIdUnpublish,
	putApiPagesById,
	putApiPagesByIdTags,
} from '../api/sdk.gen'
import {
	TAG_MAX_LENGTH,
	TAGS_PER_PAGE,
	errorMessage,
	normalizeTag,
	type PageDetail,
	type PageSummary,
} from '../lib/web-editor'
import { usePagesStore } from '../stores/pages'
import { useTagsStore } from '../stores/tags'

/**
 * Pages-list modal: slug, publish status and tags of one page. Save sends only what changed, each through its
 * own endpoint (slug, publish/unpublish, tags), in that order; a failing step stops the rest.
 */
const props = defineProps<{ page: PageSummary }>()
const emit = defineEmits<{ close: [] }>()

const pages = usePagesStore()
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
		: 'Lowercase letters, digits and single hyphens (max. 100).',
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
	let latest: PageDetail | null = null
	try {
		if (normalizedSlug.value !== props.page.slug)
			latest = (await putApiPagesById({ path: { id }, body: { slug: normalizedSlug.value } })).data
		if (published.value !== (props.page.status === 'published'))
			latest = (await (published.value ? postApiPagesByIdPublish : postApiPagesByIdUnpublish)({ path: { id } }))
				.data
		if (JSON.stringify(tags.value) !== JSON.stringify(props.page.tags)) {
			latest = (await putApiPagesByIdTags({ path: { id }, body: { tags: tags.value } })).data
			tagsStore.refresh()
		}
		if (latest) pages.remember(latest)
		emit('close')
	} catch (err: unknown) {
		// earlier steps may have saved: keep the list in sync with them
		if (latest) pages.remember(latest)
		error.value = errorMessage(err, 'Failed to save page')
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
				label="Slug"
				:error="slugError ?? undefined"
				:hint="page.locked ? 'Fixed by the site configuration' : undefined"
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
				label="Published"
				:description="published ? `Live at /${normalizedSlug}` : 'Draft: only staff can see it'"
				data-testid="page-edit-published"
			/>
			<!-- not FormField: TagInput doesn't take its label id -->
			<div class="flex flex-col gap-4">
				<span class="text-sm font-medium text-black">Tags</span>
				<TagInput
					v-model="tags"
					:suggestions="tagsStore.names"
					:popular="tagsStore.popular"
					:max="TAGS_PER_PAGE"
					:max-length="TAG_MAX_LENGTH"
					:normalize="normalizeTag"
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
				Cancel
			</Button>
			<Button
				type="submit"
				form="page-edit-form"
				:loading="saving"
				:disabled="!!slugError"
				data-testid="page-edit-save"
			>
				Save
			</Button>
		</template>
	</Modal>
</template>
