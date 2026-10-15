import { computed } from 'vue'
import { useEditorStore } from '../../../stores/editor'

/**
 * Publish rules shared by the header and the Page tab, so the two can't drift. Publishing always publishes the
 * saved copy: unsaved edits (or a save in flight) disable it. A published page can only be re-published when the
 * saved copy differs from the live one ("Publish changes").
 */
export function usePublishState() {
	const store = useEditorStore()

	const published = computed(() => store.pageStatus === 'published')
	const unsaved = computed(() => store.isDirty || store.saveStatus === 'saving')
	const publishLabel = computed(() => (published.value ? 'Publish changes' : 'Publish'))
	const canPublish = computed(
		() => !unsaved.value && !store.publishing && (!published.value || store.pageHasUnpublishedChanges),
	)
	/** Tooltip: what the button does, or why it's disabled. */
	const publishHint = computed(() => {
		if (unsaved.value) return 'Save first'
		if (published.value && !store.pageHasUnpublishedChanges) return 'No changes to publish'
		return publishLabel.value
	})
	const statusText = computed(() => {
		if (!published.value) return 'Draft'
		return store.pageHasUnpublishedChanges ? 'Published · unpublished changes' : 'Published'
	})

	return { published, canPublish, publishLabel, publishHint, statusText }
}
