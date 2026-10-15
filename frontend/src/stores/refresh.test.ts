import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePublicMenusStore } from './menus'
import { useSiteConfigStore } from './siteConfig'
import { refreshSurroundings } from '../public/refresh'
import { getApiPublicMenusByHandle, getApiPublicSiteConfig, getApiPublicState } from '../api/sdk.gen'
import type { PublicMenu, SiteConfig } from '../lib/web-editor/core/types'

vi.mock('../api/sdk.gen', () => ({
	getApiPublicMenusByHandle: vi.fn(),
	getApiPublicSiteConfig: vi.fn(),
	getApiPublicState: vi.fn(),
}))

const menu = (label: string): PublicMenu => ({
	handle: 'main',
	items: [{ label, slug: 'about', url: null, children: [] }],
})

const config = (firmName: string) => ({ fields: { firmName } }) as unknown as SiteConfig

describe('refresh (stored HTML older than a menu/config change)', () => {
	beforeEach(() => {
		setActivePinia(createPinia())
		vi.mocked(getApiPublicMenusByHandle).mockReset()
		vi.mocked(getApiPublicSiteConfig).mockReset()
	})

	it('menus: replaces the entry on success', async () => {
		const store = usePublicMenusStore()
		store.menus = { main: menu('Old') }
		vi.mocked(getApiPublicMenusByHandle).mockResolvedValue({ data: menu('New') } as never)
		await store.refresh('main')
		expect(store.menus.main).toEqual(menu('New'))
		expect(getApiPublicMenusByHandle).toHaveBeenCalledWith({ path: { handle: 'main' } })
	})

	it('menus: keeps the entry on failure', async () => {
		const store = usePublicMenusStore()
		store.menus = { main: menu('Old') }
		vi.mocked(getApiPublicMenusByHandle).mockRejectedValue(new Error('offline'))
		await expect(store.refresh('main')).resolves.toBeUndefined()
		expect(store.menus.main).toEqual(menu('Old'))
	})

	it('site config: replaces it on success', async () => {
		const store = useSiteConfigStore()
		store.set(config('Old'))
		vi.mocked(getApiPublicSiteConfig).mockResolvedValue({ data: config('New') } as never)
		await store.refresh()
		expect(store.config).toEqual(config('New'))
	})

	it('site config: keeps it on failure', async () => {
		const store = useSiteConfigStore()
		store.set(config('Old'))
		vi.mocked(getApiPublicSiteConfig).mockRejectedValue(new Error('offline'))
		await expect(store.refresh()).resolves.toBeUndefined()
		expect(store.config).toEqual(config('Old'))
	})
})

describe('refreshSurroundings (data-refresh pages: one state call)', () => {
	beforeEach(() => {
		setActivePinia(createPinia())
		vi.mocked(getApiPublicState).mockReset()
	})

	it('seeds the main menu and the site config', async () => {
		const pinia = createPinia()
		usePublicMenusStore(pinia).menus = { main: menu('Old'), footer: menu('Footer') }
		useSiteConfigStore(pinia).set(config('Old'))
		vi.mocked(getApiPublicState).mockResolvedValue({
			data: { page: null, menu: menu('New'), config: config('New') },
		} as never)
		await refreshSurroundings(pinia)
		expect(getApiPublicState).toHaveBeenCalledTimes(1)
		expect(usePublicMenusStore(pinia).menus).toEqual({ main: menu('New'), footer: menu('Footer') })
		expect(useSiteConfigStore(pinia).config).toEqual(config('New'))
	})

	it('keeps both on failure', async () => {
		const pinia = createPinia()
		usePublicMenusStore(pinia).menus = { main: menu('Old') }
		useSiteConfigStore(pinia).set(config('Old'))
		vi.mocked(getApiPublicState).mockRejectedValue(new Error('offline'))
		await expect(refreshSurroundings(pinia)).resolves.toBeUndefined()
		expect(usePublicMenusStore(pinia).menus.main).toEqual(menu('Old'))
		expect(useSiteConfigStore(pinia).config).toEqual(config('Old'))
	})
})
