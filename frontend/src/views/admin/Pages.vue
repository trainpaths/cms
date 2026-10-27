<script setup lang="ts">
import { ref, onBeforeUnmount, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useFormat } from '../../i18n'
import { Alert, Badge, Button, EmptyState, Icon, Input, Loading, useConfirm, useToast } from '@trainpaths/nb-ui'
import {
	deleteApiPagesById,
	getApiPagesById,
	postApiPages,
	postApiPagesByIdDuplicate,
	postApiPagesImport,
} from '../../api/sdk.gen'
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
const { t } = useI18n()
const format = useFormat()

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
		errorMessage.value = t('admin.pages.loadFailed')
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
		errorMessage.value = describe(err, t('admin.pages.createFailed'))
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
			message: missing
				? t('admin.pages.importedMissing', { slug: data.page.slug, count: missing }, missing)
				: t('admin.pages.imported', { slug: data.page.slug }),
			type: missing ? 'warning' : 'success',
		})
		// leave first: updating the caches re-renders the whole list (slow with thousands of pages)
		await router.push({ name: 'admin-page-editor', params: { id: data.page.id } })
		pages.add(data.page)
		tagsStore.refresh()
	} catch (err: unknown) {
		errorMessage.value = err instanceof PageExportError ? t(err.key) : describe(err, t('admin.pages.importFailed'))
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
		errorMessage.value = describe(err, t('admin.pages.exportFailed'))
	}
}

// a draft copy made by the API ("<title> (copy)", slug -2, -3…; no blocks round trip); stays on the list
async function handleDuplicatePage(page: PageSummary) {
	try {
		const { data } = await postApiPagesByIdDuplicate({ path: { id: page.id } })
		pages.add(data)
		tagsStore.refresh()
		toast({ message: t('admin.pages.duplicated', { slug: data.slug }), type: 'success' })
	} catch (err: unknown) {
		errorMessage.value = describe(err, t('admin.pages.duplicateFailed'))
	}
}

// hover intent: only a pointer resting on a row prefetches its detail, sweeping over the list fetches nothing
let hoverTimer: ReturnType<typeof setTimeout> | undefined
function hoverStart(id: string) {
	clearTimeout(hoverTimer)
	hoverTimer = setTimeout(() => pages.prefetch(id), 150)
}
function hoverEnd() {
	clearTimeout(hoverTimer)
}
onBeforeUnmount(hoverEnd)

function rowActions(page: PageSummary): ActionItem[] {
	return [
		{ label: t('common.edit'), icon: 'edit', testId: 'page-edit', onSelect: () => (editing.value = page) },
		{ label: t('admin.pages.duplicate'), icon: 'duplicate', testId: 'page-duplicate', onSelect: () => handleDuplicatePage(page) },
		{ label: t('admin.pages.export'), icon: 'download', testId: 'page-export', onSelect: () => handleExportPage(page) },
		{
			label: t('common.delete'),
			icon: 'trash',
			danger: true,
			disabled: page.locked,
			title: page.locked ? t('admin.pages.lockedDelete') : undefined,
			testId: 'page-delete',
			onSelect: () => handleDeletePage(page),
		},
	]
}

async function handleDeletePage(page: PageSummary) {
	const ok = await confirm({
		title: t('admin.pages.deleteTitle', { title: page.title }),
		message: t('common.cannotUndo'),
		confirmText: t('common.delete'),
		cancelText: t('common.cancel'),
		danger: true,
	})
	if (!ok) return
	try {
		await deleteApiPagesById({ path: { id: page.id } })
		pages.forget(page.id)
		tagsStore.refresh()
	} catch {
		errorMessage.value = t('admin.pages.deleteFailed')
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
		<h1 class="m-0 text-2xl text-gray-900">{{ $t('admin.nav.pages') }}</h1>
		<div>
			<div class="mb-12 flex items-center justify-between">
				<h2 class="mb-0 text-lg">{{ $t('admin.pages.all') }}</h2>
				<Button
					bg="primary"
					@click="showNewPageInput = true"
				>
					{{ $t('admin.pages.new') }}
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
					:title="$t('admin.pages.importHint')"
					:aria-label="$t('admin.pages.importLabel')"
					:loading="importing"
					data-testid="page-import"
					@click="importInput?.click()"
				>
					<Icon name="upload" />
					<span class="hidden sm:inline">{{ $t('admin.pages.import') }}</span>
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
					:placeholder="$t('admin.pages.titlePlaceholder')"
					:aria-label="$t('admin.pages.titleLabel')"
					@keydown.enter="handleCreatePage"
					@keydown.escape="showNewPageInput = false"
				/>
				<Button
					bg="primary"
					:disabled="!newPageTitle.trim()"
					@click="handleCreatePage"
				>
					{{ $t('admin.pages.create') }}
				</Button>
				<Button
					variant="ghost"
					square
					:title="$t('common.cancel')"
					:aria-label="$t('common.cancel')"
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
				:label="$t('admin.pages.loading')"
			/>
			<EmptyState
				v-else-if="pages.loaded && !pages.items.length"
				:title="$t('admin.pages.empty')"
				:description="$t('admin.pages.emptyHint')"
			>
				<template #action>
					<Button
						bg="primary"
						@click="showNewPageInput = true"
					>
						{{ $t('admin.pages.new') }}
					</Button>
				</template>
			</EmptyState>
			<EmptyState
				v-else-if="filtering && !filtered.length"
				icon="search"
				:title="$t('admin.pages.noMatch')"
				:description="$t('admin.pages.noMatchHint')"
			>
				<template #action>
					<Button
						variant="outline"
						@click="reset"
					>
						{{ $t('admin.pages.clearFilters') }}
					</Button>
				</template>
			</EmptyState>
			<div
				v-for="page in filtered"
				:key="page.id"
				class="mb-8 flex w-full cursor-pointer items-center rounded-md border border-gray-200 bg-white hover:bg-gray-100"
				data-testid="page-row"
				@pointerenter="hoverStart(page.id)"
				@pointerleave="hoverEnd"
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
						{{ page.status === 'published' ? $t('admin.status.published') : $t('admin.status.draft') }}
					</Badge>
					<span class="mt-2 block text-xs text-gray-400">
						/{{ page.slug }} &middot; {{ $t('admin.pages.blocks', page.blockCount) }}
						<template v-if="page.updatedAt">
							&middot; {{ $t('admin.pages.updated', { date: format.date(page.updatedAt) }) }}
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
					:title="page.status === 'published' ? $t('admin.pages.view') : $t('admin.pages.previewDraft')"
					:aria-label="page.status === 'published' ? $t('admin.pages.view') : $t('admin.pages.previewDraft')"
					data-testid="page-view"
				>
					<Icon name="eye" />
				</Button>
				<ActionMenu
					:items="rowActions(page)"
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
