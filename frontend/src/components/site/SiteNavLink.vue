<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, type RouteLocationRaw } from 'vue-router'
import type { PublicMenuItem } from '../../lib/web-editor'

/**
 * One public menu entry: page or same-site path (RouterLink), external http(s) link (new tab), or a label
 * without link (span).
 */
const props = defineProps<{ item: PublicMenuItem }>()

const route = useRoute()
const to = computed<RouteLocationRaw | null>(() => {
	const { slug, url } = props.item
	if (url?.startsWith('/')) return url
	if (!slug) return null
	return slug === 'home' ? { name: 'home' } : { name: 'public-page', params: { slug } }
})
const active = computed(
	() =>
		!!props.item.slug &&
		(route.params.slug === props.item.slug || (route.name === 'home' && props.item.slug === 'home')),
)
</script>

<template>
	<RouterLink
		v-if="to"
		:to="to"
		:aria-current="active ? 'page' : undefined"
		:class="active ? 'font-medium text-primary' : ''"
		data-testid="site-nav-link"
	>
		{{ item.label }}
	</RouterLink>
	<a
		v-else-if="item.url"
		:href="item.url"
		target="_blank"
		rel="noopener noreferrer"
		data-testid="site-nav-link"
	>
		{{ item.label }}
	</a>
	<span
		v-else
		data-testid="site-nav-folder"
		>{{ item.label }}</span
	>
</template>
