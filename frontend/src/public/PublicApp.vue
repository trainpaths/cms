<script setup lang="ts">
import { usePublicMenusStore } from '../stores/menus'
import { useSiteConfigStore } from '../stores/siteConfig'
import SiteFooter from '../components/SiteFooter.vue'
import SiteHeader from '../components/SiteHeader.vue'
import AdminBar from '../components/AdminBar.vue'

/** Root of the server-rendered public site: one layout around the route (page, not-found or customer screen). */
const menus = usePublicMenusStore()
const siteConfig = useSiteConfigStore()
</script>

<template>
	<!-- the instance's site theme (tokens overridden under .site-theme in its stylesheet) -->
	<div class="site-theme flex min-h-screen flex-col">
		<AdminBar />
		<SiteHeader
			:config="siteConfig.config"
			:menu="menus.menus.main ?? null"
		/>
		<RouterView />
		<SiteFooter :config="siteConfig.config" />
	</div>
</template>
