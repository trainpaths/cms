<script setup lang="ts">
import SiteHeader from './SiteHeader.vue'
import { usePublicMenusStore } from '../stores/menus'
import { useSiteConfigStore } from '../stores/siteConfig'

/** Public site header outside CMS pages (customer auth screens, `meta.publicNav`). */
const menus = usePublicMenusStore()
const siteConfig = useSiteConfigStore()
menus.load('main')
siteConfig.load()
</script>

<template>
	<SiteHeader
		:config="siteConfig.config"
		:menu="menus.menus.main ?? null"
	/>
</template>
