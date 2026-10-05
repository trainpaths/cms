<script setup lang="ts">
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { Icon, type IconName } from '@trainpaths/nb-ui'
import { useAuthStore } from '../stores/auth'
import { loginRouteFor } from '../router'
import { CMS_ICON } from '../public/head'

/**
 * Navigation for signed-in views: fixed left sidebar on md+ (items on top, user + log out at the bottom).
 * Below md a top bar whose burger opens the same content as a full-screen overlay.
 */

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const { user, authType } = storeToRefs(authStore)
const open = ref(false)

const items = computed(() => {
	const staff = authType.value === 'staff'
	const all: { to: string; label: string; icon: IconName; show: boolean }[] = [
		{ to: '/admin', label: 'Dashboard', icon: 'masonry', show: true },
		{ to: '/admin/pages', label: 'Pages', icon: 'file', show: staff },
		{ to: '/admin/menus', label: 'Menus', icon: 'list-tree', show: staff },
		{ to: '/admin/media', label: 'Media', icon: 'image', show: staff },
		{ to: '/admin/configuration', label: 'Configuration', icon: 'settings', show: staff },
	]
	return all.filter((item) => item.show)
})

function isActive(to: string) {
	if (to === '/admin') return route.path === '/admin'
	// change-password belongs to the user section
	if (to === '/profile') return route.path === '/profile' || route.path === '/change-password'
	return route.path === to || route.path.startsWith(`${to}/`)
}

function linkClass(to: string) {
	return isActive(to)
		? 'bg-primary/10 font-medium text-primary'
		: 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
}

async function logout() {
	const type = authType.value
	await authStore.logout()
	router.push(loginRouteFor(type))
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
			:aria-label="open ? 'Close menu' : 'Open menu'"
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
		<RouterLink
			to="/admin"
			class="hidden h-56 shrink-0 items-center gap-8 px-20 text-lg font-semibold text-primary no-underline md:flex"
		>
			<img
				:src="CMS_ICON"
				alt=""
				class="size-24"
			/>
			CMS
		</RouterLink>

		<nav
			class="p-8"
			aria-label="Admin"
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
						{{ item.label }}
					</RouterLink>
				</li>
			</ul>
		</nav>

		<div class="mt-auto flex items-center gap-4 border-t border-gray-200 p-8">
			<RouterLink
				to="/profile"
				class="flex min-w-0 flex-1 items-center gap-12 rounded-md px-12 py-10 text-sm no-underline md:py-8"
				:class="linkClass('/profile')"
				:title="user?.email ?? undefined"
				:aria-current="isActive('/profile') ? 'page' : undefined"
			>
				<Icon
					name="user"
					:size="18"
				/>
				User
			</RouterLink>
			<button
				type="button"
				class="flex size-36 shrink-0 cursor-pointer items-center justify-center rounded-md border-none bg-transparent text-gray-600 hover:bg-gray-100 hover:text-gray-900"
				title="Log out"
				aria-label="Log out"
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
