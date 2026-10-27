import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useMachineTranslation } from './useMachineTranslation'
import { useEditorStore } from '../../../stores/editor'
import { useInstanceStore } from '../../../stores/instance'
import { registerAllBlocks } from '../core/blockTypes'
import { postApiTranslate } from '../../../api/sdk.gen'

vi.mock('../../../api/sdk.gen', () => ({ getApiPagesById: vi.fn(), putApiPagesById: vi.fn(), postApiTranslate: vi.fn() }))
vi.mock('vue-i18n', async (original) => ({
	...(await original<typeof import('vue-i18n')>()),
	useI18n: () => ({ t: (key: string) => key }),
}))
const toast = vi.fn()
vi.mock('@trainpaths/nb-ui', async (original) => ({
	...(await original<typeof import('@trainpaths/nb-ui')>()),
	useToast: () => ({ toast }),
	useConfirm: () => ({ confirm: async () => true }),
}))

registerAllBlocks()

// resolves when the test says so: lets it change the store while the request runs
function pendingTranslation() {
	let answer!: (texts: string[]) => void
	vi.mocked(postApiTranslate).mockImplementationOnce(
		() =>
			new Promise((resolve) => {
				answer = (texts) => resolve({ data: { texts } } as never)
			}) as never,
	)
	return (texts: string[]) => answer(texts)
}

function load() {
	const store = useEditorStore()
	store.pageId = 'p1'
	store.pageTitle = 'About'
	store.setBlocks([
		{ id: 'a', name: 'paragraph', attributes: { text: 'One' }, innerBlocks: [] },
		{ id: 'b', name: 'paragraph', attributes: { text: 'Two' }, innerBlocks: [] },
	])
	store.editLang = 'de'
	useInstanceStore().config.machineTranslation = true
	return store
}

describe('useMachineTranslation', () => {
	beforeEach(() => {
		setActivePinia(createPinia())
		vi.mocked(postApiTranslate).mockReset()
		toast.mockReset()
	})

	it('fills every missing field of the edited language', async () => {
		const store = load()
		const answer = pendingTranslation()
		const running = useMachineTranslation().translateMissing()
		answer(['Über', 'Eins', 'Zwei'])
		await running
		expect(vi.mocked(postApiTranslate).mock.calls[0]![0]!.body).toEqual({ target: 'de', texts: ['About', 'One', 'Two'] })
		expect(store.pageTranslations.de!.title).toBe('Über')
		expect(store.findBlockById('a')!.translations!.de).toEqual({ text: 'Eins' })
	})

	it('keeps what the owner typed while the request ran', async () => {
		const store = load()
		const answer = pendingTranslation()
		const running = useMachineTranslation().translateMissing()
		store.updateBlockTranslation('b', 'de', { text: 'Mein Text' })
		answer(['Über', 'Eins', 'Zwei'])
		await running
		expect(store.findBlockById('a')!.translations!.de).toEqual({ text: 'Eins' })
		expect(store.findBlockById('b')!.translations!.de).toEqual({ text: 'Mein Text' })
	})

	it('drops the result when the page changed meanwhile', async () => {
		const store = load()
		const answer = pendingTranslation()
		const running = useMachineTranslation().translateMissing()
		store.pageId = 'p2' // same language, another page
		answer(['Über', 'Eins', 'Zwei'])
		await running
		expect(store.findBlockById('a')!.translations).toBeUndefined()
		expect(toast).not.toHaveBeenCalled()
	})

	it('a failure lands in the editor error', async () => {
		const store = load()
		vi.mocked(postApiTranslate).mockRejectedValueOnce(new Error('down'))
		await useMachineTranslation().translateMissing()
		expect(store.errorMessage).toBeTruthy()
		expect(store.findBlockById('a')!.translations).toBeUndefined()
	})
})
