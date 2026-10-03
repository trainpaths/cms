import { ref, computed, watch } from 'vue'
import { defineStore } from 'pinia'
import { nanoid } from 'nanoid'
import type { BlockInstance, BlockContext, PageDetail, PageStatus } from '../lib/web-editor/core/types'
import {
	getApiPagesById,
	postApiPagesByIdPublish,
	postApiPagesByIdUnpublish,
	putApiPagesById,
	putApiPagesByIdTags,
} from '../api/sdk.gen'
import { ApiError, errorMessage as describe } from '../api-error'
import { useMediaStore } from './media'
import { usePagesStore } from './pages'
import { useTagsStore } from './tags'
import { useIsMobile } from '../lib/web-editor/composables/useIsMobile'

export const useEditorStore = defineStore('editor', () => {
	const blocks = ref<BlockInstance[]>([])
	const pageId = ref<string | null>(null)
	const pageTitle = ref('')
	const pageSlug = ref('')
	const pageStatus = ref<PageStatus>('draft')
	const pageMetaTitle = ref('')
	const pageMetaDescription = ref('')
	// from the instance config (cms.config.json): template-rendered / fixed slug + undeletable / blocks writable
	const pageTemplate = ref<string | null>(null)
	const pageLocked = ref(false)
	const pageEditable = ref(true)
	const pageTags = ref<string[]>([])
	const loading = ref(false)
	const selectedBlockId = ref<string | null>(null)
	const isMobile = useIsMobile()
	// mobile: sidebars are full-screen modals, start closed
	const showSettingsPanel = ref(!isMobile.value)
	const showInserterSidebar = ref(!isMobile.value)
	const leftSidebarTab = ref<'blocks' | 'outline'>('blocks')
	const rightSidebarTab = ref<'page' | 'block'>('page')
	const changeCount = ref(0)
	const autosaveThreshold = ref(4)
	const saveStatus = ref<'idle' | 'saving' | 'saved'>('idle')
	const errorMessage = ref<string | null>(null)
	const history = ref<BlockInstance[][]>([])
	const historyIndex = ref(-1)
	const maxHistory = 50

	function cloneBlocks(list: BlockInstance[]): BlockInstance[] {
		return JSON.parse(JSON.stringify(list))
	}

	function pushHistory(): void {
		history.value = history.value.slice(0, historyIndex.value + 1)
		history.value.push(cloneBlocks(blocks.value))
		if (history.value.length > maxHistory) {
			history.value.shift()
		}
		historyIndex.value = history.value.length - 1
	}

	function trackChange(): void {
		pushHistory()
		changeCount.value++
		if (changeCount.value >= autosaveThreshold.value && pageId.value) {
			void savePage()
		}
	}

	const canUndo = computed(() => historyIndex.value > 0)
	const canRedo = computed(() => historyIndex.value < history.value.length - 1)

	function undo(): void {
		if (!canUndo.value) return
		historyIndex.value--
		blocks.value = cloneBlocks(history.value[historyIndex.value])
	}

	function redo(): void {
		if (!canRedo.value) return
		historyIndex.value++
		blocks.value = cloneBlocks(history.value[historyIndex.value])
	}

	function findBlockRecursive(
		id: string,
		list: BlockInstance[] = blocks.value,
	): { block: BlockInstance; parent: BlockInstance[]; index: number } | null {
		for (let i = 0; i < list.length; i++) {
			if (list[i].id === id) {
				return { block: list[i], parent: list, index: i }
			}
			const found = findBlockRecursive(id, list[i].innerBlocks)
			if (found) return found
		}
		return null
	}

	function findBlockById(id: string): BlockInstance | null {
		return findBlockRecursive(id)?.block ?? null
	}

	function addBlock(block: BlockInstance, parentId?: string, index?: number): void {
		const target = parentId ? findBlockById(parentId)?.innerBlocks : blocks.value
		if (!target) return

		if (index !== undefined) {
			target.splice(index, 0, block)
		} else {
			target.push(block)
		}
		trackChange()
	}

	function removeBlock(id: string): void {
		const result = findBlockRecursive(id)
		if (result) {
			result.parent.splice(result.index, 1)
			trackChange()
		}
	}

	function moveBlock(id: string, direction: 'up' | 'down'): void {
		const result = findBlockRecursive(id)
		if (!result) return
		const { parent, index } = result
		const newIndex = direction === 'up' ? index - 1 : index + 1
		if (newIndex < 0 || newIndex >= parent.length) return
		const [moved] = parent.splice(index, 1)
		parent.splice(newIndex, 0, moved)
		trackChange()
	}

	function moveBlockToPosition(blockId: string, targetParentId: string | null, targetIndex: number): void {
		const result = findBlockRecursive(blockId)
		if (!result) return
		result.parent.splice(result.index, 1)
		const target = targetParentId ? findBlockById(targetParentId)?.innerBlocks : blocks.value
		if (!target) return
		target.splice(targetIndex, 0, result.block)
		trackChange()
	}

	function deepCloneBlock(block: BlockInstance): BlockInstance {
		return {
			id: nanoid(),
			name: block.name,
			attributes: { ...block.attributes },
			innerBlocks: block.innerBlocks.map(deepCloneBlock),
		}
	}

	function duplicateBlock(id: string): void {
		const result = findBlockRecursive(id)
		if (!result) return
		const clone = deepCloneBlock(result.block)
		result.parent.splice(result.index + 1, 0, clone)
		selectedBlockId.value = clone.id
		trackChange()
	}

	function updateBlockAttributes(id: string, attrs: Record<string, string | number | boolean>): void {
		const block = findBlockById(id)
		if (block) {
			Object.assign(block.attributes, attrs)
			trackChange()
		}
	}

	function selectBlock(id: string | null): void {
		selectedBlockId.value = id
	}

	function clearSelection(): void {
		selectedBlockId.value = null
	}

	// mobile: one modal at a time
	function toggleSettingsPanel(): void {
		showSettingsPanel.value = !showSettingsPanel.value
		if (showSettingsPanel.value && isMobile.value) showInserterSidebar.value = false
	}

	function toggleInserterSidebar(): void {
		showInserterSidebar.value = !showInserterSidebar.value
		if (showInserterSidebar.value && isMobile.value) showSettingsPanel.value = false
	}

	/** Close the left modal on mobile (after insert / outline select, so the result is visible). */
	function closeInserterOnMobile(): void {
		if (isMobile.value) showInserterSidebar.value = false
	}

	// right sidebar follows selection: block selected → Block tab, cleared → Page tab
	watch(selectedBlockId, (id) => (rightSidebarTab.value = id ? 'block' : 'page'))

	watch(isMobile, (mobile) => {
		if (mobile && showInserterSidebar.value && showSettingsPanel.value) showSettingsPanel.value = false
	})

	function getBlockContext(id: string): BlockContext | null {
		function walk(list: BlockInstance[], parentId: string | null): BlockContext | null {
			for (let i = 0; i < list.length; i++) {
				if (list[i].id === id) {
					return { block: list[i], siblings: list, index: i, parentId }
				}
				const found = walk(list[i].innerBlocks, list[i].id)
				if (found) return found
			}
			return null
		}
		return walk(blocks.value, null)
	}

	function setBlocks(newBlocks: BlockInstance[]): void {
		blocks.value = newBlocks
	}

	function applyPage(page: PageDetail): void {
		useMediaStore().seed(page.media)
		pageId.value = page.id
		pageTitle.value = page.title
		pageSlug.value = page.slug
		pageStatus.value = page.status
		pageMetaTitle.value = page.metaTitle
		pageMetaDescription.value = page.metaDescription
		pageTemplate.value = page.template ?? null
		pageLocked.value = page.locked
		pageEditable.value = page.editable
		pageTags.value = page.tags
		blocks.value = page.blocks
		selectedBlockId.value = null
		changeCount.value = 0
		saveStatus.value = 'idle'
		history.value = [cloneBlocks(page.blocks)]
		historyIndex.value = 0
	}

	// bumped by every load/close: a slower, superseded load must not apply its page
	let loadSeq = 0

	/** Shows the cached copy at once (if any), then the server copy unless the user already edited. */
	async function loadPage(id: string): Promise<boolean> {
		const seq = ++loadSeq
		const pages = usePagesStore()
		const cached = pages.cached(id)
		if (cached) applyPage(cached)
		loading.value = !cached
		errorMessage.value = null
		try {
			const { data: fresh } = await getApiPagesById({ path: { id } })
			pages.remember(fresh)
			useMediaStore().seed(fresh.media)
			if (seq !== loadSeq) return false
			const untouched = changeCount.value === 0 && historyIndex.value === 0
			if (!cached || (untouched && fresh.updatedAt !== cached.updatedAt)) applyPage(fresh)
			// tag edits don't bump updatedAt
			else if (!tagsInFlight) pageTags.value = fresh.tags
			return true
		} catch (err: unknown) {
			const missing = err instanceof ApiError && err.status === 404
			if (missing) pages.forget(id)
			if (seq !== loadSeq) return false
			if (missing) closePage()
			errorMessage.value = missing ? 'Page not found' : 'Failed to load page'
			return false
		} finally {
			if (seq === loadSeq) loading.value = false
		}
	}

	function closePage(): void {
		loadSeq++
		loading.value = false
		pageId.value = null
		pageTitle.value = ''
		pageSlug.value = ''
		pageStatus.value = 'draft'
		pageMetaTitle.value = ''
		pageMetaDescription.value = ''
		pageTemplate.value = null
		pageLocked.value = false
		pageEditable.value = true
		pageTags.value = []
		blocks.value = []
		selectedBlockId.value = null
		changeCount.value = 0
		saveStatus.value = 'idle'
		history.value = []
		historyIndex.value = -1
	}

	// Saves are serialized: a save requested while one is in flight runs once it finishes, so an
	// older snapshot can never overwrite a newer one.
	let inFlight: Promise<void> | null = null
	let saveQueued = false

	async function savePage(): Promise<void> {
		if (!pageId.value) return
		if (inFlight) {
			saveQueued = true
			return inFlight
		}
		inFlight = (async () => {
			do {
				saveQueued = false
				const id = pageId.value
				if (!id) break
				saveStatus.value = 'saving'
				changeCount.value = 0
				try {
					const meta = { metaTitle: pageMetaTitle.value, metaDescription: pageMetaDescription.value }
					// non-editable (template-only) pages reject block writes
					const body = pageEditable.value
						? { title: pageTitle.value, ...meta, blocks: cloneBlocks(blocks.value) }
						: { title: pageTitle.value, ...meta }
					usePagesStore().remember((await putApiPagesById({ path: { id }, body })).data)
					saveStatus.value = 'saved'
				} catch (err: unknown) {
					saveStatus.value = 'idle'
					errorMessage.value = describe(err, 'Failed to save page')
					break
				}
			} while (saveQueued)
		})()
		try {
			await inFlight
		} finally {
			inFlight = null
		}
	}

	/** Slug changes are saved on their own so an invalid/taken slug never blocks content saves. */
	async function updateSlug(slug: string): Promise<boolean> {
		if (!pageId.value) return false
		try {
			const { data: page } = await putApiPagesById({ path: { id: pageId.value }, body: { slug } })
			usePagesStore().remember(page)
			pageSlug.value = page.slug
			return true
		} catch (err: unknown) {
			errorMessage.value = describe(err, 'Failed to update slug')
			return false
		}
	}

	// latest wins: changes made while a tag save is in flight are sent once it returns
	let tagsInFlight: Promise<void> | null = null

	/** Tags are saved on their own, right away (not part of content autosave / undo). */
	function updateTags(tags: string[]): Promise<void> {
		pageTags.value = tags
		tagsInFlight ??= (async () => {
			let sent: string | null = null
			while (pageId.value && JSON.stringify(pageTags.value) !== sent) {
				sent = JSON.stringify(pageTags.value)
				try {
					const { data: page } = await putApiPagesByIdTags({
						path: { id: pageId.value },
						body: { tags: [...pageTags.value] },
					})
					usePagesStore().remember(page)
				} catch (err: unknown) {
					errorMessage.value = describe(err, 'Failed to save tags')
					break
				}
			}
			useTagsStore().refresh()
		})().finally(() => (tagsInFlight = null))
		return tagsInFlight
	}

	async function setPublished(published: boolean): Promise<void> {
		if (!pageId.value) return
		// Publish what's on screen, not the last autosave.
		await savePage()
		try {
			const call = published ? postApiPagesByIdPublish : postApiPagesByIdUnpublish
			const { data: page } = await call({ path: { id: pageId.value } })
			usePagesStore().remember(page)
			pageStatus.value = page.status
		} catch (err: unknown) {
			errorMessage.value = describe(err, 'Failed to change publish status')
		}
	}

	function dismissError(): void {
		errorMessage.value = null
	}

	return {
		blocks,
		pageId,
		pageTitle,
		pageSlug,
		pageStatus,
		pageMetaTitle,
		pageMetaDescription,
		pageTemplate,
		pageLocked,
		pageEditable,
		pageTags,
		loading,
		selectedBlockId,
		showSettingsPanel,
		showInserterSidebar,
		leftSidebarTab,
		rightSidebarTab,
		changeCount,
		autosaveThreshold,
		saveStatus,
		errorMessage,
		dismissError,
		canUndo,
		canRedo,
		undo,
		redo,
		findBlockById,
		addBlock,
		removeBlock,
		moveBlock,
		moveBlockToPosition,
		duplicateBlock,
		updateBlockAttributes,
		setBlocks,
		selectBlock,
		clearSelection,
		toggleSettingsPanel,
		toggleInserterSidebar,
		closeInserterOnMobile,
		getBlockContext,
		loadPage,
		closePage,
		savePage,
		updateSlug,
		updateTags,
		setPublished,
	}
})
