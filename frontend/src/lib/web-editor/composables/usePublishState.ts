import { computed } from 'vue'
import { useConfirm } from '@trainpaths/nb-ui'
import { useEditorStore } from '../../../stores/editor'

/**
 * Publish rules shared by the header and the Page tab, so the two can't drift. Publishing always publishes the
 * saved copy: unsaved edits (or a save in flight) disable it. A published page can only be re-published when the
 * saved copy differs from the live one ("Publish changes"). `publish()` / `unpublish()` ask for confirmation first.
 */
export function usePublishState() {
	const store = useEditorStore()
	const { confirm } = useConfirm()

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

	async function publish() {
		const ok = await confirm({
			title: published.value ? 'Publish changes?' : 'Publish this page?',
			message: `Visitors at /${store.pageSlug} will see the saved version.`,
			confirmText: publishLabel.value,
		})
		if (ok) await store.publish()
	}

	async function unpublish() {
		const ok = await confirm({
			title: 'Unpublish this page?',
			message: `Visitors at /${store.pageSlug} get "page not found" until you publish again.`,
			confirmText: 'Unpublish',
			danger: true,
		})
		if (ok) await store.unpublish()
	}

	return { published, canPublish, publishLabel, publishHint, statusText, publish, unpublish }
}
