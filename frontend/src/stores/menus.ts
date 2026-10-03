import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { PublicMenu } from '../lib/web-editor/core/types'
import { getApiPublicMenusByHandle } from '../api/sdk.gen'

/** Public menus by handle (site navigation). Loaded once per session; the admin view drops them after saving. */
export const usePublicMenusStore = defineStore('publicMenus', () => {
	const menus = ref<Record<string, PublicMenu | null>>({})
	const pending = new Map<string, Promise<PublicMenu | null>>()

	/** Never throws: a missing menu just renders no navigation. */
	function load(handle: string): Promise<PublicMenu | null> {
		if (handle in menus.value) return Promise.resolve(menus.value[handle]!)
		let request = pending.get(handle)
		if (!request) {
			request = getApiPublicMenusByHandle({ path: { handle } })
				.then(({ data }) => data)
				.catch(() => null)
				.then((menu) => (menus.value[handle] = menu))
				.finally(() => pending.delete(handle))
			pending.set(handle, request)
		}
		return request
	}

	function invalidate(): void {
		menus.value = {}
	}

	return { menus, load, invalidate }
})
