import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useConfirm, useToast } from '@trainpaths/nb-ui'
import { postApiTranslate } from '../../../api/sdk.gen'
import { errorMessage } from '../../../api-error'
import { useEditorStore } from '../../../stores/editor'
import { useInstanceStore } from '../../../stores/instance'
import { DEFAULT_LANGUAGE } from '../../../i18n/languages'
import { textFields } from '../core/translatable'

// one request at a time across the editor (header hint + Page tab)
const running = ref(false)

/**
 * DeepL pre-fill of the edited language (`store.editLang`): `translateMissing` sends the default-language text of
 * every untranslated field, `retranslateAll` (after a confirm) every field. Results land in the working copy as one
 * undo step; the owner reviews, saves and publishes as usual. `available` = the site has a key and a non-default
 * language is being edited.
 */
export function useMachineTranslation() {
	const store = useEditorStore()
	const instance = useInstanceStore()
	const { t } = useI18n()
	const { toast } = useToast()
	const { confirm } = useConfirm()

	const available = computed(
		() => instance.config.machineTranslation && store.editLang !== DEFAULT_LANGUAGE.code && store.pageEditable,
	)

	async function run(missingOnly: boolean): Promise<void> {
		if (running.value || !available.value) return
		const code = store.editLang
		const page = { title: store.pageTitle, metaTitle: store.pageMetaTitle, metaDescription: store.pageMetaDescription }
		const fields = textFields(store.blocks, page, store.pageTranslations, missingOnly ? code : undefined)
		if (!fields.length) {
			toast({ message: t('editor.deepl.nothing'), type: 'info' })
			return
		}
		running.value = true
		try {
			const { data } = await postApiTranslate({ body: { target: code, texts: fields.map((f) => f.source) } })
			// the owner may have switched language or page meanwhile: apply to the one it was asked for
			if (store.editLang !== code) return
			store.applyTranslations(
				code,
				fields.map((field, i) => ({ blockId: field.blockId, key: field.key, text: data.texts[i] ?? '' })),
			)
			toast({ message: t('editor.deepl.done', fields.length), type: 'success' })
		} catch (err: unknown) {
			store.errorMessage = errorMessage(err, t('editor.deepl.failed'))
		} finally {
			running.value = false
		}
	}

	async function retranslateAll(): Promise<void> {
		const ok = await confirm({
			title: t('editor.deepl.retranslateTitle'),
			message: t('editor.deepl.retranslateMessage'),
			confirmText: t('editor.deepl.retranslate'),
			cancelText: t('common.cancel'),
			danger: true,
		})
		if (ok) await run(false)
	}

	return { available, running, translateMissing: () => run(true), retranslateAll }
}
