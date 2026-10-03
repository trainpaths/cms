<script setup lang="ts">
import type { PublicMenuItem } from '../../lib/web-editor'
import SiteNavLink from './SiteNavLink.vue'

/** Nested menu items as an indented list (inside desktop dropdowns and the mobile panel). */
defineProps<{ items: PublicMenuItem[] }>()
</script>

<template>
	<ul class="m-0 list-none p-0">
		<li
			v-for="(item, index) in items"
			:key="index"
		>
			<SiteNavLink
				:item="item"
				class="block rounded px-12 py-6 text-sm no-underline"
				:class="item.slug || item.url ? 'text-gray-700 hover:bg-gray-100 hover:text-gray-900' : 'font-medium text-gray-900'"
			/>
			<SiteNavTree
				v-if="item.children.length"
				:items="item.children"
				class="ml-12 border-l border-gray-200 pl-4"
			/>
		</li>
	</ul>
</template>
