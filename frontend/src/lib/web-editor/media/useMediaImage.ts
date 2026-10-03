import { computed } from 'vue'
import type { BlockInstance } from '../core/types'
import { useMediaStore } from '../../../stores/media'

/**
 * Resolves the media asset referenced by the block's `mediaId` attribute (the one attribute the API
 * resolves into the page's `media[]`). Alt comes from the media object, fallback "image".
 */
export function useMediaImage(block: () => BlockInstance) {
	const media = useMediaStore()

	const mediaId = computed(() => (block().attributes.mediaId as string) || '')
	const asset = computed(() => (mediaId.value ? media.get(mediaId.value) : null))

	const src = computed(() => asset.value?.url ?? '')
	const alt = computed(() => asset.value?.alt || 'image')
	/** Referenced media that no longer exists (deleted from the library). */
	const missing = computed(() => !!mediaId.value && !asset.value)

	return { mediaId, asset, src, alt, missing }
}
