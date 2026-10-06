<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { Tabs } from '@trainpaths/nb-ui'
import { useAuthStore } from '../stores/auth'
import ProfileDetails from '../components/ProfileDetails.vue'
import BackupsPanel from '../components/backups/BackupsPanel.vue'

const route = useRoute()
const router = useRouter()
const { user, authType } = storeToRefs(useAuthStore())

// the API enforces it too (SuperAdmin policy); this only hides the tab
const isSuperAdmin = computed(() => authType.value === 'staff' && !!user.value?.roles?.includes('super_admin'))
const tabs = [
	{ key: 'profile', label: 'Profile' },
	{ key: 'backups', label: 'Backups' },
]
// in the query so a reload or link lands on the same tab
const tab = computed({
	get: () => (route.query.tab === 'backups' ? 'backups' : 'profile'),
	set: (key: string) => router.replace({ query: { ...route.query, tab: key === 'profile' ? undefined : key } }),
})
</script>

<template>
	<div
		class="mx-auto p-32 font-sans"
		:class="isSuperAdmin ? 'max-w-800' : 'max-w-600'"
	>
		<h1 class="mb-24 text-2xl font-bold">Profile</h1>
		<Tabs
			v-if="isSuperAdmin"
			v-model="tab"
			:tabs="tabs"
			data-testid="profile-tabs"
		>
			<template #profile>
				<ProfileDetails />
			</template>
			<template #backups>
				<BackupsPanel v-if="tab === 'backups'" />
			</template>
		</Tabs>
		<ProfileDetails v-else />
	</div>
</template>
