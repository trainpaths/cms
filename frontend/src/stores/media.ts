import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { MediaItem, MediaRef } from '../lib/web-editor/core/types'
import { deleteApiMediaById, getApiMedia, postApiMedia, putApiMediaById } from '../api/sdk.gen'
import { ApiError } from '../api-error'
import { MEDIA_MAX_BYTES } from '../lib/web-editor/limits'

/**
 * Media library + by-id cache. Blocks only store a media id; they render from `byId`, which is seeded
 * from page responses (`seed`) and kept current by library actions, so an alt edit shows everywhere.
 */
export const useMediaStore = defineStore('media', () => {
	const byId = ref<Record<string, MediaRef>>({})
	const items = ref<MediaItem[]>([])
	const loaded = ref(false)
	const loading = ref(false)

	function remember(media: MediaRef): void {
		byId.value[media.id] = { id: media.id, url: media.url, alt: media.alt }
	}

	function seed(refs: MediaRef[]): void {
		refs.forEach(remember)
	}

	function get(id: string): MediaRef | null {
		return byId.value[id] ?? null
	}

	/** Loads the library once; `force` refetches. Throws ApiError. */
	async function load(force = false): Promise<void> {
		if ((loaded.value && !force) || loading.value) return
		loading.value = true
		try {
			items.value = (await getApiMedia()).data
			items.value.forEach(remember)
			loaded.value = true
		} finally {
			loading.value = false
		}
	}

	async function upload(file: File, alt?: string): Promise<MediaItem> {
		// fail before uploading 10 MB just to get a 413
		if (file.size > MEDIA_MAX_BYTES) throw new ApiError('File exceeds 10 MB.', 413)
		const { data: item } = await postApiMedia({ body: { file, alt } })
		items.value.unshift(item)
		remember(item)
		return item
	}

	/** `alt` is always sent (the API clears it when omitted); `fileName` only renames the display name. */
	async function update(id: string, body: { alt: string; fileName?: string }): Promise<void> {
		const { data: item } = await putApiMediaById({ path: { id }, body })
		const index = items.value.findIndex((m) => m.id === id)
		if (index >= 0) items.value[index] = item
		remember(item)
	}

	async function remove(id: string): Promise<void> {
		await deleteApiMediaById({ path: { id } })
		items.value = items.value.filter((m) => m.id !== id)
		delete byId.value[id]
	}

	return { byId, items, loaded, loading, seed, get, load, upload, update, remove }
})
