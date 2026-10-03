import { useEditorStore } from '../../../stores/editor'

/** Editor only: points the block's `mediaId` at a media asset ('' clears it). */
export function assignMedia(blockId: string, mediaId: string): void {
	useEditorStore().updateBlockAttributes(blockId, { mediaId })
}
