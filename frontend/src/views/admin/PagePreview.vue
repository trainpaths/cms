<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Link, Loading } from '@trainpaths/nb-ui'
import { getApiPagesById } from '../../api/sdk.gen'
import type { PageDetail } from '../../lib/web-editor'
import PageView from '../PageView.vue'
import SiteFooter from '../../components/SiteFooter.vue'
import SiteHeader from '../../components/SiteHeader.vue'
import { usePublicMenusStore } from '../../stores/menus'
import { useSiteConfigStore } from '../../stores/siteConfig'

const props = defineProps<{ id: string }>()

const page = ref<PageDetail | null>(null)
const failed = ref(false)
const siteConfig = useSiteConfigStore()
const menus = usePublicMenusStore()
menus.load('main')

onMounted(async () => {
	siteConfig.load()
	try {
		page.value = (await getApiPagesById({ path: { id: props.id } })).data
	} catch {
		failed.value = true
	}
})
</script>

<template>
	<div class="border-b border-yellow-200 bg-yellow-50 px-16 py-8 text-center text-sm text-yellow-800 font-sans">
		Preview{{ page?.status === 'draft' ? ' of an unpublished draft' : '' }} &middot;
		<Link :to="`/admin/pages/${id}`">Back to editor</Link>
	</div>
	<div
		v-if="page"
		class="site-theme"
	>
		<SiteHeader
			:config="siteConfig.config"
			:menu="menus.menus.main ?? null"
		/>
		<PageView
			:page="page"
			:config="siteConfig.config"
		/>
		<SiteFooter :config="siteConfig.config" />
	</div>
	<p
		v-else-if="failed"
		class="p-32 text-center text-gray-500"
	>
		Page not found.
	</p>
	<Loading
		v-else
		label="Loading preview…"
	/>
</template>
