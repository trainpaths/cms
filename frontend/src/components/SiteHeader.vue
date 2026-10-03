<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import type { PublicMenu, SiteConfig } from '../lib/web-editor'
import SiteNavLink from './site/SiteNavLink.vue'
import SiteNavTree from './site/SiteNavTree.vue'

/**
 * Public site header: the owner's logo (→ home) and the main menu. md+: top-level items in a row, children in a
 * dropdown (hover / keyboard focus / tap), deeper levels indented inside it. Below md: burger → panel with the
 * whole tree. Nothing is rendered when there is neither a logo nor menu items.
 */
const props = defineProps<{ config: SiteConfig | null; menu: PublicMenu | null }>()

const route = useRoute()
const open = ref(false)
const logo = computed(() => props.config?.logo ?? null)
const items = computed(() => props.menu?.items ?? [])
const visible = computed(() => !!logo.value || items.value.length > 0)

watch(
	() => route.fullPath,
	() => (open.value = false),
)

// Escape closes a focus-opened dropdown
function onKeydown(e: KeyboardEvent) {
	if (e.key === 'Escape') (document.activeElement as HTMLElement | null)?.blur()
}
</script>

<template>
	<header
		v-if="visible"
		class="relative z-30 border-b border-gray-200 bg-white font-sans"
		data-testid="site-header"
	>
		<div class="mx-auto flex min-h-64 max-w-5xl items-center justify-between gap-24 px-24">
			<RouterLink
				:to="{ name: 'home' }"
				class="flex shrink-0 items-center py-8"
				aria-label="Home"
			>
				<img
					v-if="logo"
					:src="logo.url"
					:alt="logo.alt || 'Logo'"
					class="max-h-40 max-w-160 object-contain"
					data-testid="site-header-logo"
				/>
				<span
					v-else
					class="text-base font-semibold text-gray-900"
					>Home</span
				>
			</RouterLink>

			<nav
				v-if="items.length"
				class="hidden md:block"
				aria-label="Main"
				data-testid="site-nav"
				@keydown="onKeydown"
			>
				<ul class="m-0 flex list-none items-center gap-4 p-0">
					<li
						v-for="(item, index) in items"
						:key="index"
						class="group relative"
						data-testid="site-nav-item"
					>
						<div class="flex items-center">
							<SiteNavLink
								:item="item"
								class="rounded-md px-12 py-8 text-sm text-gray-700 no-underline hover:bg-gray-100 hover:text-gray-900"
							/>
							<!-- focusable toggle: opens the dropdown by keyboard/tap (focus-within) -->
							<button
								v-if="item.children.length"
								type="button"
								class="-ml-8 cursor-pointer rounded-md border-none bg-transparent px-6 py-8 text-xs text-gray-500 hover:text-gray-900"
								:aria-label="`${item.label} submenu`"
								aria-haspopup="true"
								data-testid="site-nav-toggle"
							>
								&#9662;
							</button>
						</div>
						<div
							v-if="item.children.length"
							class="invisible absolute left-0 top-full min-w-200 rounded-md border border-gray-200 bg-white p-6 opacity-0 shadow-lg transition-opacity group-focus-within:visible group-focus-within:opacity-100 group-hover:visible group-hover:opacity-100"
							data-testid="site-nav-dropdown"
						>
							<SiteNavTree :items="item.children" />
						</div>
					</li>
				</ul>
			</nav>

			<button
				v-if="items.length"
				type="button"
				class="flex size-40 cursor-pointer items-center justify-center rounded-md border-none bg-transparent text-gray-600 hover:bg-gray-100 md:hidden"
				:aria-expanded="open"
				aria-controls="site-nav-mobile"
				:aria-label="open ? 'Close menu' : 'Open menu'"
				data-testid="site-nav-burger"
				@click="open = !open"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					width="22"
					height="22"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					stroke-linecap="round"
				>
					<path
						v-if="open"
						d="M6 6l12 12M18 6L6 18"
					/>
					<path
						v-else
						d="M4 7h16M4 12h16M4 17h16"
					/>
				</svg>
			</button>
		</div>

		<nav
			v-if="open"
			id="site-nav-mobile"
			class="absolute inset-x-0 top-full max-h-[calc(100vh-64px)] overflow-y-auto border-b border-gray-200 bg-white px-16 py-12 shadow-lg md:hidden"
			aria-label="Main"
			data-testid="site-nav-mobile"
		>
			<SiteNavTree :items="items" />
		</nav>
	</header>
</template>
