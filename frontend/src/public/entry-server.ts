import { createMemoryHistory } from 'vue-router'
import { renderToString } from 'vue/server-renderer'
import { createPublicApp } from './createPublicApp'
import { headTags, serializeState } from './head'
import { pathOf, type PublicState } from './state'
import type { Rendered } from '../../server/template'

export type { PublicState }

/** Renders one public page; the render server splices the parts into the `public.html` template. */
export async function render(state: PublicState): Promise<Rendered> {
	const { app, router } = createPublicApp(state, createMemoryHistory(), true)
	// not-found renders under any single-segment path; the client keeps its own URL (START_LOCATION)
	await router.push(state.page ? pathOf(state.slug) : '/not-found')
	await router.isReady()
	return { head: headTags(state), html: await renderToString(app), state: serializeState(state) }
}
