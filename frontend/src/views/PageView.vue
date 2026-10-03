<script setup lang="ts">
import { computed, watch } from 'vue'
import PageContent from './PageContent.vue'
import { getTemplate, type TemplatePage } from '../public/templates'
import { useMediaStore } from '../stores/media'
import type { SiteConfig } from '../lib/web-editor'

/**
 * A page's main content: its instance template (`src/templates/<page.template>.vue`) if the instance config gives it
 * one, else the default layout (`PageContent`). Public site + admin preview.
 */
const props = defineProps<{ page: TemplatePage; config: SiteConfig | null }>()

const template = computed(() => getTemplate(props.page.template))

// image blocks resolve their mediaId from the store, also in templates that skip PageContent
const mediaStore = useMediaStore()
watch(
	() => props.page.media,
	(media) => mediaStore.seed(media),
	{ immediate: true },
)
</script>

<template>
	<component
		:is="template"
		v-if="template"
		:page="page"
		:config="config"
	/>
	<PageContent
		v-else
		:title="page.title"
		:blocks="page.blocks"
		:media="page.media"
	/>
</template>
