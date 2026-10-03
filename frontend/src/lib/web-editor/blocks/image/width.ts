import { computed } from 'vue'
import type { BlockInstance } from '../../core/types'
import { useEditorStore } from '../../../../stores/editor'

/** Image width in percent (1–100). Also reads legacy string values like "70%"; unparsable → 100. */
export function widthPercent(raw: unknown): number {
	const n = typeof raw === 'number' ? raw : parseFloat(String(raw ?? ''))
	if (!Number.isFinite(n) || n <= 0) return 100
	return Math.min(100, Math.round(n))
}

/** Writable computed over the image `width` attribute, normalised to a percent number. */
export function useImageWidth(block: () => BlockInstance) {
	const store = useEditorStore()
	return computed({
		get: () => widthPercent(block().attributes.width),
		set: (value: number) => store.updateBlockAttributes(block().id, { width: widthPercent(value) }),
	})
}
