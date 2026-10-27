<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { Button, Icon, Loading, useConfirm, useToast, useUnsavedChanges } from '@trainpaths/nb-ui'
import { I18nT, useI18n } from 'vue-i18n'
import UnsavedDialog from '../../components/UnsavedDialog.vue'
import { deleteApiMenusById, getApiMenus, postApiMenus, putApiMenusById } from '../../api/sdk.gen'
import {
	MENU_HANDLE_MAX,
	MENU_MAX_ITEMS,
	errorMessage as describe,
	normalizeHandle,
	type MenuDetail,
	type MenuItem,
	type PageSummary,
} from '../../lib/web-editor'
import { countItems } from '../../lib/menus/tree'
import MenuTree from '../../components/menus/MenuTree.vue'
import { newItem, pageItem } from '../../components/menus/context'
import { useMenuDrag } from '../../components/menus/useMenuDrag'
import PageFilters from '../../components/PageFilters.vue'
import { usePageFilter } from '../../composables/usePageFilter'
import { usePagesStore } from '../../stores/pages'
import { useTagsStore } from '../../stores/tags'
import { usePublicMenusStore } from '../../stores/menus'

const MAIN = 'main'

const toast = useToast()
const { t } = useI18n()
const pages = usePagesStore()
const tagsStore = useTagsStore()
const publicMenus = usePublicMenusStore()
const drag = useMenuDrag()

// every menu with its items (one GET): switching menus needs no fetch
const menus = ref<MenuDetail[]>([])
const current = ref<MenuDetail | null>(null)
const items = ref<MenuItem[]>([])
const saved = ref('')
const loaded = ref(false)
const loadFailed = ref(false)
const saving = ref(false)
const creating = ref(false)
const errorMessage = ref<string | null>(null)
const newHandle = ref('')
const showNewMenu = ref(false)
const newHandlePreview = computed(() => normalizeHandle(newHandle.value))
const editor = ref<HTMLElement | null>(null)

const published = computed(() => pages.items.filter((p) => p.status === 'published'))
const { search, tag, sort, filtered } = usePageFilter(published)

const snapshot = () => JSON.stringify(items.value)
const dirty = computed(() => !!current.value && snapshot() !== saved.value)
const isMain = computed(() => current.value?.handle === MAIN)
const full = computed(() => countItems(items.value) >= MENU_MAX_ITEMS)

// same rule as the API's MenuService.IsMenuUrl
const isMenuUrl = (url: string) => /^https?:\/\/\S+$/i.test(url) || /^\/(?![/\\])\S*$/.test(url)

/** Problems the API would reject; shown per row, block saving. */
const errors = computed(() => {
	const result = new Map<string, string>()
	const walk = (list: MenuItem[]) =>
		list.forEach((item) => {
			if (item.url != null && !isMenuUrl(item.url.trim()))
				result.set(item.id, t('admin.menus.badUrl'))
			else if (!item.label.trim() && !item.pageId) result.set(item.id, t('admin.menus.labelRequired'))
			walk(item.children)
		})
	walk(items.value)
	return result
})
const valid = computed(() => errors.value.size === 0)

function apply(menu: MenuDetail) {
	current.value = menu
	items.value = JSON.parse(JSON.stringify(menu.items))
	saved.value = snapshot()
}

/** Returns whether the menu is saved afterwards (the unsaved-changes dialog continues only then). */
async function save(): Promise<boolean> {
	const menu = current.value
	if (!menu || !valid.value || saving.value) return false
	saving.value = true
	errorMessage.value = null
	try {
		const { data } = await putApiMenusById({ path: { id: menu.id }, body: { items: items.value } })
		apply(data)
		menus.value[menus.value.findIndex((m) => m.id === data.id)] = data
		publicMenus.invalidate()
		toast.toast({ message: t('admin.menus.saved'), type: 'success' })
		return true
	} catch (err: unknown) {
		errorMessage.value = describe(err, t('admin.menus.saveFailed'))
		return false
	} finally {
		saving.value = false
	}
}

const unsaved = useUnsavedChanges(dirty, save)

async function select(id: string) {
	if (current.value?.id === id || !(await unsaved.confirmLeave())) return
	const menu = menus.value.find((m) => m.id === id)
	if (!menu) return
	errorMessage.value = null
	apply(menu)
}

onMounted(async () => {
	pages.load().catch(() => {})
	tagsStore.load()
	try {
		menus.value = (await getApiMenus()).data
		loaded.value = true
		if (menus.value[0]) await select(menus.value[0].id)
	} catch {
		loadFailed.value = true
	}
})

async function createMenu() {
	const handle = newHandlePreview.value
	if (!handle || creating.value || !(await unsaved.confirmLeave())) return
	creating.value = true
	try {
		const { data: menu } = await postApiMenus({ body: { handle } })
		menus.value.push(menu)
		newHandle.value = ''
		showNewMenu.value = false
		apply(menu)
	} catch (err: unknown) {
		errorMessage.value = describe(err, t('admin.menus.createFailed'))
	} finally {
		creating.value = false
	}
}

const { confirm } = useConfirm()

async function deleteMenu() {
	const menu = current.value
	if (!menu || isMain.value) return
	const ok = await confirm({
		title: t('admin.menus.deleteTitle', { handle: menu.handle }),
		message: t('common.cannotUndo'),
		confirmText: t('common.delete'),
		cancelText: t('common.cancel'),
		danger: true,
	})
	if (!ok) return
	try {
		await deleteApiMenusById({ path: { id: menu.id } })
		menus.value = menus.value.filter((m) => m.id !== menu.id)
		current.value = null
		saved.value = ''
		if (menus.value[0]) await select(menus.value[0].id)
	} catch (err: unknown) {
		errorMessage.value = describe(err, t('admin.menus.deleteFailed'))
	}
}

async function addItem() {
	if (full.value) return
	items.value.push(newItem())
	await nextTick()
	const labels = editor.value?.querySelectorAll<HTMLInputElement>('[data-testid="menu-item-label"]')
	labels?.[labels.length - 1]?.focus()
}

function addPage(page: PageSummary) {
	if (!full.value) items.value.push(pageItem(page))
}

const inMenu = computed(() => {
	const ids = new Set<string>()
	const walk = (list: MenuItem[]) => list.forEach((i) => (i.pageId && ids.add(i.pageId), walk(i.children)))
	walk(items.value)
	return ids
})

onBeforeRouteLeave(() => unsaved.confirmLeave())
</script>

<template>
	<div class="mx-auto max-w-6xl px-16 py-24 font-sans">
		<div class="mb-4 flex items-center justify-between gap-12">
			<h1 class="m-0 text-2xl text-gray-900">{{ $t('admin.nav.menus') }}</h1>
			<div
				v-if="current"
				class="flex items-center gap-12"
			>
				<span
					v-if="dirty"
					class="text-xs text-gray-500"
					>{{ $t('admin.unsavedChanges') }}</span
				>
				<Button
					:loading="saving"
					:disabled="!dirty || !valid"
					data-testid="menu-save"
					@click="save"
				>
					{{ $t('common.save') }}
				</Button>
			</div>
		</div>
		<I18nT
			keypath="admin.menus.intro"
			tag="p"
			class="mb-16 text-xs text-gray-500"
			scope="global"
		>
			<template #main>
				<strong>{{ $t('admin.menus.mainMenu') }}</strong>
			</template>
		</I18nT>

		<div
			v-if="errorMessage"
			class="mb-16 flex items-center justify-between gap-12 rounded-md border border-red-200 bg-red-50 px-16 py-8 text-sm text-red-700"
			role="alert"
		>
			<span>{{ errorMessage }}</span>
			<button
				type="button"
				class="cursor-pointer border-none bg-transparent text-red-400 hover:text-red-600"
				:aria-label="$t('admin.dismiss')"
				@click="errorMessage = null"
			>
				&#10005;
			</button>
		</div>

		<p
			v-if="loadFailed"
			class="text-sm text-red-600"
		>
			{{ $t('admin.menus.loadFailed') }}
		</p>
		<Loading
			v-else-if="!loaded"
			:label="$t('admin.menus.loading')"
		/>

		<div
			v-else
			class="grid grid-cols-1 gap-16 lg:grid-cols-[200px_1fr_300px] lg:items-start"
		>
			<!-- menu list -->
			<nav
				class="rounded-lg border border-gray-200 bg-white p-8 shadow-xs"
				:aria-label="$t('admin.nav.menus')"
			>
				<ul class="m-0 flex list-none flex-col gap-2 p-0">
					<li
						v-for="menu in menus"
						:key="menu.id"
					>
						<button
							type="button"
							class="flex w-full cursor-pointer items-center justify-between gap-8 rounded-md border-none px-8 py-6 text-left text-sm"
							:class="
								current?.id === menu.id
									? 'bg-primary/10 font-medium text-primary'
									: 'bg-transparent text-gray-700 hover:bg-gray-100'
							"
							data-testid="menu-list-item"
							@click="select(menu.id)"
						>
							<span class="truncate font-mono">{{ menu.handle }}</span>
							<span
								v-if="menu.handle === MAIN"
								class="shrink-0 rounded-full bg-primary px-6 py-1 text-[10px] font-medium text-white"
								>{{ $t('admin.menus.main') }}</span
							>
						</button>
					</li>
				</ul>
				<form
					v-if="showNewMenu"
					class="mt-8 flex flex-col gap-6"
					@submit.prevent="createMenu"
				>
					<input
						v-model="newHandle"
						class="input-field mt-0 font-mono"
						:maxlength="MENU_HANDLE_MAX"
						:disabled="creating"
						:placeholder="$t('admin.menus.handlePlaceholder')"
						:aria-label="$t('admin.menus.newHandle')"
						data-testid="menu-new-handle"
						@keydown.escape="showNewMenu = false"
					/>
					<p
						v-if="newHandlePreview && newHandlePreview !== newHandle"
						class="m-0 text-xs text-gray-500"
					>
						<I18nT
							keypath="admin.menus.savedAs"
							scope="global"
						>
							<template #handle>
								<span class="font-mono">{{ newHandlePreview }}</span>
							</template>
						</I18nT>
					</p>
					<div class="flex gap-6">
						<Button
							size="sm"
							type="submit"
							:disabled="!newHandlePreview"
							:loading="creating"
							data-testid="menu-new-create"
							>{{ $t('admin.pages.create') }}</Button
						>
						<Button
							size="sm"
							type="button"
							variant="ghost"
							text="primary"
							:disabled="creating"
							@click="showNewMenu = false"
							>{{ $t('common.cancel') }}</Button
						>
					</div>
				</form>
				<button
					v-else
					type="button"
					class="mt-8 w-full cursor-pointer rounded border border-dashed border-gray-300 bg-white px-8 py-6 text-sm text-gray-700 hover:border-primary hover:text-primary"
					data-testid="menu-new"
					@click="showNewMenu = true"
				>
					{{ $t('admin.menus.new') }}
				</button>
			</nav>

			<!-- editor -->
			<section
				class="min-w-0 rounded-lg border border-gray-200 bg-white p-16 shadow-xs"
				data-testid="menu-editor"
			>
				<template v-if="current">
					<h2
						class="m-0 mb-16 font-mono text-base font-semibold text-gray-900"
						:title="$t('admin.menus.handleHint')"
						data-testid="menu-handle"
					>
						{{ current.handle }}
					</h2>
					<MenuTree
						v-model="items"
						:pages="pages.items"
						:errors="errors"
					/>
					<div class="flex flex-wrap items-center gap-8">
						<button
							type="button"
							class="cursor-pointer rounded border border-dashed border-gray-400 bg-white px-12 py-6 text-sm text-gray-700 hover:border-primary hover:text-primary disabled:cursor-not-allowed disabled:opacity-60"
							:disabled="full"
							data-testid="menu-add-item"
							@click="addItem"
						>
							{{ $t('admin.menus.addItem') }}
						</button>
						<button
							v-if="!isMain"
							type="button"
							class="ml-auto cursor-pointer rounded border-none bg-transparent px-8 py-6 text-sm text-red-600 hover:bg-red-50"
							data-testid="menu-delete"
							@click="deleteMenu"
						>
							{{ $t('admin.menus.delete') }}
						</button>
					</div>
					<p class="mb-0 mt-12 text-xs text-gray-500">
						{{ $t('admin.menus.dragHint') }}
					</p>
				</template>
			</section>

			<!-- page picker -->
			<aside
				class="rounded-lg border border-gray-200 bg-white p-12 shadow-xs"
				data-testid="menu-page-picker"
			>
				<h2 class="m-0 mb-8 text-sm font-semibold text-gray-900">{{ $t('admin.menus.publishedPages') }}</h2>
				<PageFilters
					v-model:search="search"
					v-model:tag="tag"
					v-model:sort="sort"
					:tags="tagsStore.names"
					compact
				/>
				<Loading
					v-if="!pages.loaded"
					:label="$t('common.loading')"
				/>
				<p
					v-else-if="!filtered.length"
					class="mb-0 mt-12 text-sm text-gray-400"
				>
					{{ $t('admin.menus.noPages') }}
				</p>
				<ul class="m-0 mt-12 flex max-h-[60vh] list-none flex-col gap-4 overflow-y-auto p-0">
					<li
						v-for="page in filtered"
						:key="page.id"
						:draggable="!!current"
						class="flex cursor-grab items-center gap-8 rounded-md border border-gray-300 bg-white px-8 py-6"
						:title="$t('admin.menus.pickerHint')"
						data-testid="menu-picker-page"
						@dragstart="drag.start($event, { kind: 'page', pageId: page.id })"
						@dragend="drag.end"
					>
						<div class="min-w-0 flex-1">
							<div class="truncate text-sm text-gray-800">{{ page.title }}</div>
							<div class="truncate text-xs text-gray-400">/{{ page.slug }}</div>
						</div>
						<span
							v-if="inMenu.has(page.id)"
							class="shrink-0 text-[11px] text-gray-400"
							:title="$t('admin.menus.inMenu')"
							>&#10003;</span
						>
						<button
							type="button"
							class="flex size-32 shrink-0 cursor-pointer items-center justify-center rounded-md border border-gray-300 bg-white text-gray-700 hover:border-primary hover:text-primary disabled:cursor-not-allowed disabled:opacity-60"
							:disabled="!current || full"
							:title="$t('admin.menus.addPage', { title: page.title })"
							:aria-label="$t('admin.menus.addPage', { title: page.title })"
							data-testid="menu-picker-add"
							@click="addPage(page)"
						>
							<Icon
								name="plus"
								:size="18"
							/>
						</button>
					</li>
				</ul>
			</aside>
		</div>
	</div>
	<UnsavedDialog
		v-if="unsaved.prompting.value"
		:saving="saving"
		:can-save="valid"
		@save="unsaved.answer('save')"
		@discard="unsaved.answer('discard')"
		@cancel="unsaved.answer('cancel')"
	/>
</template>
