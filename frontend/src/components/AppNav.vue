<script setup lang="ts">
import { ref, watch, onBeforeUnmount } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { Icon, type IconName } from '@trainpaths/nb-ui'
import { useAuthStore } from '../stores/auth'
import { CMS_ICON } from '../public/head'
import { IS_LOCALIZED } from '../i18n'
import LanguageDropdown from './LanguageDropdown.vue'

/**
 * Navigation for signed-in views: fixed left sidebar on md+ (items on top, user + language + log out at the bottom).
 * Below md a top bar whose burger opens the same content as a full-screen overlay.
 */

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const { user } = storeToRefs(authStore)
const open = ref(false)

const items: { to: string; label: string; icon: IconName }[] = [
	{ to: '/admin', label: 'admin.nav.dashboard', icon: 'masonry' },
	{ to: '/admin/pages', label: 'admin.nav.pages', icon: 'file' },
	{ to: '/admin/menus', label: 'admin.nav.menus', icon: 'list-tree' },
	{ to: '/admin/media', label: 'admin.nav.media', icon: 'image' },
	{ to: '/admin/configuration', label: 'admin.nav.configuration', icon: 'settings' },
]

function isActive(to: string) {
	if (to === '/admin') return route.path === '/admin'
	// change-password belongs to the user section
	if (to === '/admin/profile') return route.path === '/admin/profile' || route.path === '/admin/change-password'
	return route.path === to || route.path.startsWith(`${to}/`)
}

function linkClass(to: string) {
	return isActive(to)
		? 'bg-primary/10 font-medium text-primary'
		: 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
}

async function logout() {
	await authStore.logout()
	router.push({ name: 'admin-login' })
}

watch(
	() => route.fullPath,
	() => (open.value = false),
)

// overlay covers the page: keep the page behind it from scrolling
watch(open, (value) => (document.body.style.overflow = value ? 'hidden' : ''))
onBeforeUnmount(() => (document.body.style.overflow = ''))
</script>

<template>
	<!-- small screens: top bar with burger -->
	<header
		class="sticky top-0 z-40 flex h-56 items-center justify-between border-b border-gray-200 bg-white px-16 font-sans md:hidden"
	>
		<RouterLink
			to="/admin"
			class="flex items-center gap-8 text-lg font-semibold text-primary no-underline"
		>
			<img
				:src="CMS_ICON"
				alt=""
				class="size-24"
			/>
			CMS
		</RouterLink>
		<button
			type="button"
			class="flex size-36 cursor-pointer items-center justify-center rounded-md border-none bg-transparent text-gray-600 hover:bg-gray-100"
			:aria-expanded="open"
			aria-controls="app-nav-menu"
			:aria-label="open ? $t('site.closeMenu') : $t('site.openMenu')"
			@click="open = !open"
			>
			<Icon
			:name="open ? 'x' : 'menu'"
			:size="20"
			/>
		</button>
	</header>

	<!-- md+: sidebar; below md: full-screen overlay under the top bar -->
	<aside
		id="app-nav-menu"
		class="fixed inset-x-0 bottom-0 top-56 z-40 flex-col overflow-y-auto overscroll-contain bg-white font-sans md:inset-x-auto md:left-0 md:top-0 md:flex md:w-220 md:border-r md:border-gray-200"
		:class="open ? 'flex' : 'hidden'"
		@keydown.escape="open = false"
	>
		<div class="hidden h-56 shrink-0 items-center pl-20 md:flex">
			<RouterLink
				to="/admin"
				class="flex items-center gap-8 text-lg font-semibold text-primary no-underline"
			>
				<img
					:src="CMS_ICON"
					alt=""
					class="size-24"
				/>
				CMS
			</RouterLink>
		</div>

		<nav
			class="p-8"
			:aria-label="$t('admin.nav.label')"
		>
			<ul class="m-0 flex list-none flex-col gap-2 p-0">
				<li
					v-for="item in items"
					:key="item.to"
				>
					<RouterLink
						:to="item.to"
						class="flex items-center gap-12 rounded-md px-12 py-10 text-sm no-underline md:py-8"
						:class="linkClass(item.to)"
						:aria-current="isActive(item.to) ? 'page' : undefined"
					>
						<Icon
							:name="item.icon"
							:size="18"
						/>
						{{ $t(item.label) }}
					</RouterLink>
				</li>
			</ul>
		</nav>

		<div class="mt-auto flex items-center gap-4 border-t border-gray-200 p-8">
			<RouterLink
				to="/admin/profile"
				class="flex min-w-0 flex-1 items-center gap-12 rounded-md px-12 py-10 text-sm no-underline md:py-8"
				:class="linkClass('/admin/profile')"
				:title="user?.email ?? undefined"
				:aria-current="isActive('/admin/profile') ? 'page' : undefined"
			>
				<Icon
					name="user"
					:size="18"
				/>
				{{ $t('admin.nav.user') }}
			</RouterLink>
			<!-- end: the sidebar scrolls (overflow), a start-aligned menu would stick out of it -->
			<LanguageDropdown
				v-if="IS_LOCALIZED"
				align="end"
			/>
			<button
				type="button"
				class="flex h-36 w-28 shrink-0 cursor-pointer items-center justify-center rounded-md border-none bg-transparent text-gray-600 hover:bg-gray-100 hover:text-gray-900"
				:title="$t('common.logOut')"
				:aria-label="$t('common.logOut')"
				data-testid="app-nav-logout"
				@click="logout"
			>
				<Icon
					name="exit"
					:size="18"
				/>
			</button>
		</div>
	</aside>
</template>
