<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { storeToRefs } from 'pinia'
import { Button, Card, Link } from '@trainpaths/nb-ui'
import BackupsSummary from '../components/backups/BackupsSummary.vue'

const router = useRouter()
const authStore = useAuthStore()
const { user, authType } = storeToRefs(authStore)
// backups API is super admin only
const isSuperAdmin = computed(() => authType.value === 'staff' && !!user.value?.roles?.includes('super_admin'))

async function logout() {
	await authStore.logout()
	router.push({ name: 'admin-login' })
}
</script>

<template>
	<div class="p-32 max-w-600 mx-auto font-sans">
		<h1 class="text-2xl font-bold mb-24">Dashboard</h1>

		<div class="mb-24 flex flex-wrap items-center justify-between gap-16">
			<p class="m-0 text-black opacity-70">Welcome, {{ user?.displayName || user?.email || 'User' }}!</p>
			<Button
				variant="outline"
				data-testid="dashboard-logout"
				@click="logout"
			>
				Log Out
			</Button>
		</div>

		<Card data-testid="dashboard-pages">
			<template #header>
				<div class="flex items-center justify-between gap-16">
					<h3 class="m-0 text-base font-semibold text-black">Pages</h3>
					<Link
						to="/admin/pages"
						data-testid="dashboard-pages-link"
						>&rarr; Go to Pages</Link
					>
				</div>
			</template>
			<p class="m-0 text-sm text-black/60">Create, edit and publish the pages of your website.</p>
		</Card>

		<BackupsSummary
			v-if="isSuperAdmin"
			class="mt-24"
		/>
	</div>
</template>
