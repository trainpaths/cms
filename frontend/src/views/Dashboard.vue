<script setup lang="ts">
import { computed } from 'vue'
import { useAuthStore } from '../stores/auth'
import { storeToRefs } from 'pinia'
import { Link } from '@trainpaths/nb-ui'
import BackupsSummary from '../components/backups/BackupsSummary.vue'

const authStore = useAuthStore()
const { user, authType } = storeToRefs(authStore)
// backups API is super admin only
const isSuperAdmin = computed(() => authType.value === 'staff' && !!user.value?.roles?.includes('super_admin'))
</script>

<template>
	<div class="p-32 max-w-600 mx-auto font-sans">
		<h1 class="text-2xl font-bold mb-24">Dashboard</h1>

		<p class="text-black opacity-70 mb-16">Welcome, {{ user?.displayName || user?.email || 'User' }}!</p>

		<div class="flex gap-16">
			<Link to="/profile">View Profile</Link>
			<Link to="/change-password">Change Password</Link>
		</div>

		<p class="mt-24 text-sm text-black opacity-50">Logged in as {{ authType }}</p>

		<BackupsSummary
			v-if="isSuperAdmin"
			class="mt-24"
		/>
	</div>
</template>
