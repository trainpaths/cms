<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Alert, Badge, Button, EmptyState, Input, Loading, useConfirm } from '@trainpaths/nb-ui'
import { deleteApiPagesById, postApiPages } from '../../api/sdk.gen'
import { errorMessage as describe, type PageSummary } from '../../lib/web-editor'
import { usePagesStore } from '../../stores/pages'
import { useTagsStore } from '../../stores/tags'
import { usePageFilter } from '../../composables/usePageFilter'
import PageFilters from '../../components/PageFilters.vue'
import PageEditDialog from '../../components/PageEditDialog.vue'

const router = useRouter()

// cached list renders at once; load() refreshes it in the background
const pages = usePagesStore()
const errorMessage = ref<string | null>(null)
const newPageTitle = ref('')
const showNewPageInput = ref(false)
const tagsStore = useTagsStore()
const { search, tag, status, sort, active: filtering, filtered, reset } = usePageFilter(() => pages.items)
const editing = ref<PageSummary | null>(null)
const { confirm } = useConfirm()

onMounted(async () => {
	tagsStore.load()
	try {
		await pages.load()
	} catch {
		errorMessage.value = 'Failed to load pages'
	}
})

async function handleCreatePage() {
	const title = newPageTitle.value.trim()
	if (!title) return
	try {
		const { data: page } = await postApiPages({ body: { title } })
		pages.add(page)
		await router.push({ name: 'admin-page-editor', params: { id: page.id } })
	} catch (err: unknown) {
		errorMessage.value = describe(err, 'Failed to create page')
	}
}

// drafts have no public URL: open the staff preview instead
function viewHref(page: PageSummary): string {
	return page.status === 'published'
		? router.resolve({ name: 'public-page', params: { slug: page.slug } }).href
		: router.resolve({ name: 'admin-page-preview', params: { id: page.id } }).href
}

async function handleDeletePage(page: PageSummary) {
	const ok = await confirm({
		title: `Delete "${page.title}"?`,
		message: 'This cannot be undone.',
		confirmText: 'Delete',
		danger: true,
	})
	if (!ok) return
	try {
		await deleteApiPagesById({ path: { id: page.id } })
		pages.forget(page.id)
		tagsStore.refresh()
	} catch {
		errorMessage.value = 'Failed to delete page'
	}
}
</script>

<template>
	<div
		v-if="errorMessage"
		class="fixed left-1/2 top-16 z-50 -translate-x-1/2 transform"
	>
		<Alert
			type="error"
			dismissible
			class="shadow-lg"
			@dismiss="errorMessage = null"
		>
			{{ errorMessage }}
		</Alert>
	</div>

	<div class="mx-auto max-w-3xl px-16 py-24 font-sans">
		<h1 class="m-0 text-2xl text-gray-900">Pages</h1>
		<div>
			<div class="mb-12 flex items-center justify-between">
				<h2 class="mb-0 text-lg">All pages</h2>
				<Button
					bg="primary"
					@click="showNewPageInput = true"
				>
					+ New Page
				</Button>
			</div>
			<div
				v-if="showNewPageInput"
				class="mb-12 flex gap-8"
			>
				<Input
					v-model="newPageTitle"
					class="flex-1"
					placeholder="Page title..."
					aria-label="Page title"
					@keydown.enter="handleCreatePage"
					@keydown.escape="showNewPageInput = false"
				/>
				<Button
					bg="primary"
					:disabled="!newPageTitle.trim()"
					@click="handleCreatePage"
				>
					Create
				</Button>
				<Button
					variant="ghost"
					@click="showNewPageInput = false"
				>
					Cancel
				</Button>
			</div>
			<PageFilters
				v-if="pages.items.length"
				v-model:search="search"
				v-model:tag="tag"
				v-model:status="status"
				v-model:sort="sort"
				:tags="tagsStore.names"
				class="mb-12"
			/>
			<Loading
				v-if="!pages.loaded && !errorMessage"
				label="Loading pages…"
			/>
			<EmptyState
				v-else-if="pages.loaded && !pages.items.length"
				title="No pages yet."
				description="Create the first page of your website."
			>
				<template #action>
					<Button
						bg="primary"
						@click="showNewPageInput = true"
					>
						+ New Page
					</Button>
				</template>
			</EmptyState>
			<EmptyState
				v-else-if="filtering && !filtered.length"
				icon="search"
				title="No pages match."
				description="Try another search or filter."
			>
				<template #action>
					<Button
						variant="outline"
						@click="reset"
					>
						Clear filters
					</Button>
				</template>
			</EmptyState>
			<div
				v-for="page in filtered"
				:key="page.id"
				class="mb-8 flex w-full cursor-pointer items-center rounded-md border border-gray-200 bg-white hover:bg-gray-100"
				data-testid="page-row"
				@pointerenter="pages.prefetch(page.id)"
				@focusin="pages.prefetch(page.id)"
			>
				<RouterLink
					:to="{ name: 'admin-page-editor', params: { id: page.id } }"
					class="flex-1 p-12 text-left text-inherit no-underline"
				>
					<span class="text-[15px]">{{ page.title }}</span>
					<Badge
						size="sm"
						dot
						:color="page.status === 'published' ? 'success' : 'secondary'"
						class="ml-8 align-middle"
					>
						{{ page.status === 'published' ? 'Published' : 'Draft' }}
					</Badge>
					<span class="mt-2 block text-xs text-gray-400">
						/{{ page.slug }} &middot; {{ page.blockCount }} blocks
						<template v-if="page.updatedAt">
							&middot; updated {{ new Date(page.updatedAt).toLocaleDateString() }}
						</template>
					</span>
					<span
						v-if="page.tags.length"
						class="mt-6 flex flex-wrap gap-4"
					>
						<Badge
							v-for="name in page.tags"
							:key="name"
							size="sm"
							data-testid="page-row-tag"
							>{{ name }}</Badge
						>
					</span>
				</RouterLink>
				<Button
					variant="ghost"
					text="secondary"
					square
					title="Edit slug, status and tags"
					aria-label="Edit page settings"
					data-testid="page-edit"
					@click="editing = page"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						width="16"
						height="16"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						stroke-linecap="round"
						stroke-linejoin="round"
					>
						<path d="M12 20h9" />
						<path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
					</svg>
				</Button>
				<Button
					as="a"
					variant="ghost"
					text="secondary"
					square
					:href="viewHref(page)"
					target="_blank"
					:title="page.status === 'published' ? 'View page' : 'Preview draft'"
					:aria-label="page.status === 'published' ? 'View page' : 'Preview draft'"
					data-testid="page-view"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						width="16"
						height="16"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						stroke-linecap="round"
						stroke-linejoin="round"
					>
						<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
						<circle
							cx="12"
							cy="12"
							r="3"
						/>
					</svg>
				</Button>
				<Button
					variant="ghost"
					text="danger"
					square
					class="mr-8"
					:title="page.locked ? 'Part of the site configuration, can\'t be deleted' : 'Delete page'"
					aria-label="Delete page"
					:disabled="page.locked"
					data-testid="page-delete"
					@click="handleDeletePage(page)"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						width="16"
						height="16"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						stroke-linecap="round"
						stroke-linejoin="round"
					>
						<polyline points="3 6 5 6 21 6" />
						<path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
					</svg>
				</Button>
			</div>
		</div>
	</div>
	<PageEditDialog
		v-if="editing"
		:page="editing"
		@close="editing = null"
	/>
</template>
