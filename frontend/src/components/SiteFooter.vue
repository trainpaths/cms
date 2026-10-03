<script setup lang="ts">
import { computed } from 'vue'
import type { SiteConfig } from '../lib/web-editor'
import { filledEntries, firmName as firmNameOf } from '../lib/siteConfig'
import SiteConfigEntry from './SiteConfigEntry.vue'

/**
 * Public site footer (default; instances override it to place their own groups, e.g. socials): the owner's logo,
 * firm name and filled contact entries (address as lines, mailto/tel/link by type), then the published footer pages
 * (instance config `"footer": true`). Nothing is rendered when all of these are empty.
 */
const props = defineProps<{ config: SiteConfig | null }>()

const firmName = computed(() => firmNameOf(props.config))
const contact = computed(() => filledEntries(props.config, 'contact'))
const logo = computed(() => props.config?.logo ?? null)
const links = computed(() => props.config?.footerLinks ?? [])
const hasContent = computed(() => !!logo.value || !!firmName.value || contact.value.length > 0)
const visible = computed(() => hasContent.value || links.value.length > 0)
</script>

<template>
	<footer
		v-if="visible"
		class="mt-auto border-t border-gray-200 bg-gray-50 font-sans"
		data-testid="site-footer"
	>
		<div
			v-if="hasContent"
			class="mx-auto flex max-w-5xl flex-col gap-24 px-24 py-32 md:flex-row md:gap-48"
		>
			<div class="flex flex-col gap-12 self-start">
				<img
					v-if="logo"
					:src="logo.url"
					:alt="logo.alt || 'Logo'"
					class="max-h-48 max-w-200 object-contain"
					data-testid="site-footer-logo"
				/>
				<p
					v-if="firmName"
					class="m-0 text-sm font-semibold text-gray-900"
					data-testid="site-footer-firm"
				>
					{{ firmName }}
				</p>
			</div>
			<dl class="m-0 grid flex-1 grid-cols-1 gap-x-32 gap-y-16 sm:grid-cols-2 lg:grid-cols-3">
				<div
					v-for="entry in contact"
					:key="entry.id"
					class="min-w-0"
					data-testid="site-footer-entry"
				>
					<dt class="text-xs font-medium uppercase tracking-wide text-gray-500">{{ entry.label }}</dt>
					<dd class="m-0 mt-2 text-sm text-gray-800">
						<SiteConfigEntry :entry="entry" />
					</dd>
				</div>
			</dl>
		</div>
		<nav
			v-if="links.length"
			class="mx-auto flex max-w-5xl flex-wrap gap-x-24 gap-y-8 px-24 py-16 text-xs"
			:class="{ 'border-t border-gray-200': hasContent }"
			aria-label="Legal"
		>
			<RouterLink
				v-for="link in links"
				:key="link.slug"
				:to="{ name: 'public-page', params: { slug: link.slug } }"
				class="text-gray-500 no-underline hover:text-gray-800 hover:underline"
				data-testid="site-footer-link"
			>
				{{ link.title }}
			</RouterLink>
		</nav>
	</footer>
</template>
