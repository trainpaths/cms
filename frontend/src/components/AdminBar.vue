<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Icon } from '@trainpaths/nb-ui'
import { readAuthType, useAuthStore } from '../stores/auth'
import { CMS_ICON } from '../public/head'

/**
 * Thin bar above the public site for signed-in staff: link to the admin, log out. Client-only (shown after
 * mount from the persisted `auth_type` hint, no API call), so server render + hydration stay the same for everyone.
 */
const visible = ref(false)
const busy = ref(false)

onMounted(() => (visible.value = readAuthType() === 'staff'))

async function logout() {
	busy.value = true
	// staff logout endpoint revokes via the refresh cookie, no access token needed
	await useAuthStore().logout()
	busy.value = false
	visible.value = false
}
</script>

<template>
	<div v-if="visible" class="justify-center flex items-center h-30 bg-accent-dark font-cms text-xs text-white"
		data-testid="admin-bar">
		<div class="w-full max-w-5xl flex items-center justify-between">
			<!-- plain links: admin routes are a full page load from the public app -->
			<a href="/admin" class="flex items-center gap-6 font-semibold text-white no-underline">
				<img :src="CMS_ICON" alt="" class="size-16" />
				CMS
			</a>
			<div class="flex items-center gap-4 *:h-32">
				<a href="/admin" class="flex items-center px-8 text-white no-underline hover:bg-white/15">
					Admin
				</a>
				<button type="button"
					class="flex cursor-pointer items-center gap-6 border-none bg-transparent px-8 text-xs text-white hover:bg-white/15 disabled:opacity-60"
					:disabled="busy" data-testid="admin-bar-logout" @click="logout">
					<Icon name="exit" :size="14" />
					Log out
				</Button>
			</div>
		</div>
	</div>
</template>
