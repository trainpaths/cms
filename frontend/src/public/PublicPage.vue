<script setup lang="ts">
import { usePublicPageStore } from './store'
import { useSiteConfigStore } from '../stores/siteConfig'
import PageView from '../views/PageView.vue'
import NotFound from '../views/NotFound.vue'

/** Route component of pages: the store's current page (loaded before the navigation), or not-found. */
const store = usePublicPageStore()
const siteConfig = useSiteConfigStore()
</script>

<template>
	<PageView
		v-if="store.page"
		:key="store.slug"
		:page="store.page"
		:config="siteConfig.config"
	/>
	<NotFound v-else />
</template>
