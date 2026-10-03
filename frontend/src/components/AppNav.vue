<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '../stores/auth'

/** Top navigation for signed-in views; collapses into a burger menu below md. */

const route = useRoute()
const { user, authType } = storeToRefs(useAuthStore())
const open = ref(false)

const items = computed(() => {
	const staff = authType.value === 'staff'
	return [
		{ to: '/admin', label: 'Dashboard', show: true },
		{ to: '/admin/pages', label: 'Pages', show: staff },
		{ to: '/admin/menus', label: 'Menus', show: staff },
		{ to: '/admin/media', label: 'Media', show: staff },
		{ to: '/admin/configuration', label: 'Configuration', show: staff },
		{ to: '/profile', label: 'User', show: true },
	].filter((item) => item.show)
})

function isActive(to: string) {
	if (to === '/admin') return route.path === '/admin'
	// change-password belongs to the user section
	if (to === '/profile') return route.path === '/profile' || route.path === '/change-password'
	return route.path === to || route.path.startsWith(`${to}/`)
}

watch(
	() => route.fullPath,
	() => (open.value = false),
)
</script>

<template>
	<header class="sticky top-0 z-30 border-b border-gray-200 bg-white font-sans">
		<nav class="mx-auto flex h-56 max-w-5xl items-center justify-between px-16">
			<RouterLink
				to="/admin"
				class="text-lg font-semibold text-primary no-underline"
			>
				CMS
			</RouterLink>

			<ul class="m-0 hidden list-none items-center gap-4 p-0 md:flex">
				<li
					v-for="item in items"
					:key="item.to"
				>
					<RouterLink
						:to="item.to"
						class="rounded-md px-12 py-6 text-sm no-underline"
						:class="
							isActive(item.to)
								? 'bg-primary/10 font-medium text-primary'
								: 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
						"
						:title="item.to === '/profile' ? (user?.email ?? undefined) : undefined"
					>
						{{ item.label }}
					</RouterLink>
				</li>
			</ul>

			<button
				type="button"
				class="flex size-36 cursor-pointer items-center justify-center rounded-md border-none bg-transparent text-gray-600 hover:bg-gray-100 md:hidden"
				:aria-expanded="open"
				aria-controls="app-nav-menu"
				:aria-label="open ? 'Close menu' : 'Open menu'"
				@click="open = !open"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					width="20"
					height="20"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					stroke-linecap="round"
				>
					<template v-if="open">
						<line
							x1="6"
							y1="6"
							x2="18"
							y2="18"
						/>
						<line
							x1="18"
							y1="6"
							x2="6"
							y2="18"
						/>
					</template>
					<template v-else>
						<line
							x1="4"
							y1="7"
							x2="20"
							y2="7"
						/>
						<line
							x1="4"
							y1="12"
							x2="20"
							y2="12"
						/>
						<line
							x1="4"
							y1="17"
							x2="20"
							y2="17"
						/>
					</template>
				</svg>
			</button>
		</nav>

		<!-- small screens: dropdown panel under the bar -->
		<ul
			v-if="open"
			id="app-nav-menu"
			class="m-0 flex list-none flex-col gap-2 border-t border-gray-200 p-8 md:hidden"
		>
			<li
				v-for="item in items"
				:key="item.to"
			>
				<RouterLink
					:to="item.to"
					class="block rounded-md px-12 py-10 text-sm no-underline"
					:class="
						isActive(item.to) ? 'bg-primary/10 font-medium text-primary' : 'text-gray-700 hover:bg-gray-100'
					"
				>
					{{ item.label }}
				</RouterLink>
			</li>
		</ul>
	</header>
</template>
