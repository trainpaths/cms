<script setup lang="ts">
import { usePublicPageStore } from './store'
import { usePublicMenusStore } from '../stores/menus'
import { useSiteConfigStore } from '../stores/siteConfig'
import PageView from '../views/PageView.vue'
import NotFound from '../views/NotFound.vue'
import SiteFooter from '../components/SiteFooter.vue'
import SiteHeader from '../components/SiteHeader.vue'

/** Root of the server-rendered public site: one layout, the routes only switch the page in the store. */
const store = usePublicPageStore()
const menus = usePublicMenusStore()
const siteConfig = useSiteConfigStore()
</script>

<template>
	<!-- the instance's site theme (tokens overridden under .site-theme in its stylesheet) -->
	<div class="site-theme flex min-h-screen flex-col">
		<SiteHeader
			:config="siteConfig.config"
			:menu="menus.menus.main ?? null"
		/>
		<PageView
			v-if="store.page"
			:key="store.slug"
			:page="store.page"
			:config="siteConfig.config"
		/>
		<NotFound v-else />
		<SiteFooter :config="siteConfig.config" />
	</div>
</template>
