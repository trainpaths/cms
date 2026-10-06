<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Alert, Badge, Button, EmptyState, Icon, Input, Loading, useConfirm, useToast } from '@trainpaths/nb-ui'
import { deleteApiPagesById, getApiPagesById, postApiPages, postApiPagesImport } from '../../api/sdk.gen'
import { errorMessage as describe, type PageSummary } from '../../lib/web-editor'
import { usePagesStore } from '../../stores/pages'
import { useTagsStore } from '../../stores/tags'
import { usePageFilter } from '../../composables/usePageFilter'
import PageFilters from '../../components/PageFilters.vue'
import PageEditDialog from '../../components/PageEditDialog.vue'
import { PageExportError, pageExportFileName, parsePageExport, toPageExport } from '../../lib/pageExport'
import { saveJson } from '../../lib/download'
import ActionMenu, { type ActionItem } from '../../components/ActionMenu.vue'

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
const { toast } = useToast()
const importInput = ref<HTMLInputElement | null>(null)
const importing = ref(false)

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

// always a new draft, never overwrites (slug deduplicated by the API)
async function handleImportFile(event: Event) {
	const input = event.target as HTMLInputElement
	const file = input.files?.[0]
	input.value = '' // same file can be picked again
	if (!file) return
	importing.value = true
	try {
		const body = parsePageExport(await file.text())
		const { data } = await postApiPagesImport({ body })
		const missing = data.missingMediaIds.length
		toast({
			message:
				`Imported as draft /${data.page.slug}` +
				(missing ? `; ${missing} image${missing === 1 ? '' : 's'} not found in the media library` : ''),
			type: missing ? 'warning' : 'success',
		})
		// leave first: updating the caches re-renders the whole list (slow with thousands of pages)
		await router.push({ name: 'admin-page-editor', params: { id: data.page.id } })
		pages.add(data.page)
		tagsStore.refresh()
	} catch (err: unknown) {
		errorMessage.value = err instanceof PageExportError ? err.message : describe(err, 'Failed to import page')
	} finally {
		importing.value = false
	}
}

// the saved page, fetched fresh (the list only has summaries)
async function handleExportPage(page: PageSummary) {
	try {
		const { data } = await getApiPagesById({ path: { id: page.id } })
		pages.remember(data)
		saveJson(toPageExport(data), pageExportFileName(data.slug))
	} catch (err: unknown) {
		errorMessage.value = describe(err, 'Failed to export page')
	}
}

// a new draft with the same content via the import endpoint (slug gets -2, -3…); stays on the list
async function handleDuplicatePage(page: PageSummary) {
	try {
		const { data: source } = await getApiPagesById({ path: { id: page.id } })
		const { data } = await postApiPagesImport({
			body: {
				title: `${source.title} (copy)`.slice(0, 200),
				slug: source.slug,
				blocks: source.blocks,
				metaTitle: source.metaTitle,
				metaDescription: source.metaDescription,
				tags: source.tags,
			},
		})
		pages.add(data.page)
		tagsStore.refresh()
		toast({ message: `Duplicated as draft /${data.page.slug}`, type: 'success' })
	} catch (err: unknown) {
		errorMessage.value = describe(err, 'Failed to duplicate page')
	}
}

function rowActions(page: PageSummary): ActionItem[] {
	return [
		{ label: 'Edit', icon: 'edit', testId: 'page-edit', onSelect: () => (editing.value = page) },
		{ label: 'Duplicate', icon: 'copy', testId: 'page-duplicate', onSelect: () => handleDuplicatePage(page) },
		{ label: 'Export', icon: 'download', testId: 'page-export', onSelect: () => handleExportPage(page) },
		{
			label: 'Delete',
			icon: 'trash',
			danger: true,
			disabled: page.locked,
			title: page.locked ? "Part of the site configuration, can't be deleted" : undefined,
			testId: 'page-delete',
			onSelect: () => handleDeletePage(page),
		},
	]
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
				<Button
					variant="outline"
					text="primary"
					border="primary"
					class="gap-6"
					title="Create a draft page from an exported page file (.json)"
					aria-label="Import page"
					:loading="importing"
					data-testid="page-import"
					@click="importInput?.click()"
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
						aria-hidden="true"
					>
						<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
						<polyline points="7 10 12 15 17 10" />
						<line
							x1="12"
							y1="15"
							x2="12"
							y2="3"
						/>
					</svg>
					<span class="hidden sm:inline">Import</span>
				</Button>
				<input
					ref="importInput"
					type="file"
					accept="application/json,.json"
					class="hidden"
					data-testid="page-import-file"
					@change="handleImportFile"
				/>
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
					square
					title="Cancel"
					aria-label="Cancel"
					data-testid="page-new-cancel"
					@click="showNewPageInput = false"
				>
					<Icon name="x" />
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
				<ActionMenu
					:items="rowActions(page)"
					:label="`Actions for ${page.title}`"
					test-id="page-actions"
					class="mr-2"
				/>
			</div>
		</div>
	</div>
	<PageEditDialog
		v-if="editing"
		:page="editing"
		@close="editing = null"
	/>
</template>
