<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Button, useToast } from '@trainpaths/nb-ui'
import { getApiPublicStateBySlug } from '@trainpaths/cms/src/api/sdk.gen.ts'
import type { PublicPage } from '@trainpaths/cms/src/api/types.gen.ts'
import SiteFooter from '@trainpaths/cms/src/components/SiteFooter.vue'
import SiteHeader from '@trainpaths/cms/src/components/SiteHeader.vue'
import PageView from '@trainpaths/cms/src/views/PageView.vue'
import { documentTitle } from '@trainpaths/cms/src/public/head.ts'
import { stripLocale } from '@trainpaths/cms/src/public/locale.ts'
import { usePublicMenusStore } from '@trainpaths/cms/src/stores/menus.ts'
import { useSiteConfigStore } from '@trainpaths/cms/src/stores/siteConfig.ts'
import { translate } from '@trainpaths/cms/src/i18n/index.ts'
import { useDemo } from './context'

/** The demo page as visitors see it: published version, site header + footer, in the chosen language. */
const route = useRoute()
const router = useRouter()
const { toast } = useToast()
const { siteLang, changeLanguage } = useDemo()
const menus = usePublicMenusStore()
const siteConfig = useSiteConfigStore()

const page = ref<PublicPage | null>(null)
const loaded = ref(false)
const slug = computed(() => (typeof route.params.slug === 'string' ? route.params.slug : 'home'))

let seq = 0
async function load() {
	const current = ++seq
	const { data } = await getApiPublicStateBySlug({ path: { slug: slug.value }, query: { lang: siteLang.value } })
	if (current !== seq) return
	page.value = data.page ?? null
	menus.menus = { main: data.menu ?? null }
	siteConfig.set(data.config)
	loaded.value = true
	document.title = documentTitle(page.value, data.config, translate('site.notFound.title'))
}
watch([slug, siteLang], load, { immediate: true })

// Link blocks render plain same-site hrefs (`/about`, `/de/edit`); the demo has one page, so route them here.
// Router links (menu, logo) handle their own clicks first.
function onClick(e: MouseEvent) {
	if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return
	const anchor = (e.target as Element | null)?.closest('a')
	const href = anchor?.getAttribute('href')
	if (!anchor || !href || anchor.target === '_blank' || !href.startsWith('/') || href.startsWith('//')) return
	e.preventDefault()
	const { code, path } = stripLocale(href.split(/[?#]/)[0]!)
	if (code !== siteLang.value) void changeLanguage(code)
	if (path === '/' || path === '/home' || path === `/${page.value?.slug}`) void router.push('/')
	else if (path === '/edit') void router.push('/edit')
	else toast.info(translate('demo.onlyPage'))
}
</script>

<template>
	<div
		class="site-theme flex min-h-screen flex-col"
		@click="onClick"
	>
		<SiteHeader
			:config="siteConfig.config"
			:menu="menus.menus.main ?? null"
		/>
		<PageView
			v-if="page"
			:key="`${page.slug}-${siteLang}`"
			:page="page"
			:config="siteConfig.config"
		/>
		<div
			v-else-if="loaded"
			class="mx-auto flex max-w-480 flex-col items-center gap-16 p-32 text-center font-sans"
			data-testid="demo-not-published"
		>
			<p class="m-0 text-gray-600">{{ $t('demo.notPublished') }}</p>
			<Button
				bg="primary"
				@click="router.push('/edit')"
				>{{ $t('demo.edit') }}</Button
			>
		</div>
		<SiteFooter :config="siteConfig.config" />
	</div>
</template>
