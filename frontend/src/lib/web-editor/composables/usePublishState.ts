import { computed } from 'vue'
import { useConfirm } from '@trainpaths/nb-ui'
import { useI18n } from 'vue-i18n'
import { useEditorStore } from '../../../stores/editor'

/**
 * Publish rules shared by the header and the Page tab, so the two can't drift. Publishing always publishes the
 * saved copy: unsaved edits (or a save in flight) disable it. A published page can only be re-published when the
 * saved copy differs from the live one ("Publish changes"). `publish()` / `unpublish()` ask for confirmation first.
 */
export function usePublishState() {
	const store = useEditorStore()
	const { confirm } = useConfirm()
	const { t } = useI18n()

	const published = computed(() => store.pageStatus === 'published')
	const unsaved = computed(() => store.isDirty || store.saveStatus === 'saving')
	const publishLabel = computed(() => (published.value ? t('editor.publish.changes') : t('editor.publish.publish')))
	const canPublish = computed(
		() => !unsaved.value && !store.publishing && (!published.value || store.pageHasUnpublishedChanges),
	)
	/** Tooltip: what the button does, or why it's disabled. */
	const publishHint = computed(() => {
		if (unsaved.value) return t('editor.publish.saveFirst')
		if (published.value && !store.pageHasUnpublishedChanges) return t('editor.publish.noChanges')
		return publishLabel.value
	})
	const statusText = computed(() => {
		if (!published.value) return t('admin.status.draft')
		return store.pageHasUnpublishedChanges ? t('editor.publish.pending') : t('admin.status.published')
	})

	async function publish() {
		const ok = await confirm({
			title: published.value ? t('editor.publish.changesTitle') : t('editor.publish.title'),
			message: t('editor.publish.message', { slug: store.pageSlug }),
			confirmText: publishLabel.value,
			cancelText: t('common.cancel'),
		})
		if (ok) await store.publish()
	}

	async function unpublish() {
		const ok = await confirm({
			title: t('editor.publish.unpublishTitle'),
			message: t('editor.publish.unpublishMessage', { slug: store.pageSlug }),
			confirmText: t('editor.publish.unpublish'),
			cancelText: t('common.cancel'),
			danger: true,
		})
		if (ok) await store.unpublish()
	}

	return { published, canPublish, publishLabel, publishHint, statusText, publish, unpublish }
}
