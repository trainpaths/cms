import { createMemoryHistory } from 'vue-router'
import { renderToString } from 'vue/server-renderer'
import { createPublicApp } from './createPublicApp'
import { headTags, serializeState } from './head'
import { screenOf } from './screens'
import { pathOf, type PublicState } from './state'
import { localeBase } from './locale'
import { languageOf } from '../i18n/languages'
import type { Rendered } from '../../server/template'

export type { PublicState }

/**
 * Renders one public page or customer screen in `state.lang`; the render server splices the parts into the
 * `public.html` template (`tag` → `<html lang>`).
 */
export async function render(state: PublicState): Promise<Rendered> {
	// base = language prefix: router links resolve to /de/... like in the browser
	const { app, router, i18n } = createPublicApp(state, createMemoryHistory(localeBase(state.lang)), true)
	// not-found renders under any path; the client keeps its own URL (START_LOCATION)
	const screen = screenOf(state)
	await router.push(state.page ? pathOf(state.slug) : screen ? `/${screen.slug}` : '/not-found')
	await router.isReady()
	return {
		head: headTags(state, i18n.global.t),
		html: await renderToString(app),
		state: serializeState(state),
		tag: languageOf(state.lang).tag,
	}
}
