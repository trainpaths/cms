<script setup lang="ts">
/** Account details (display name edit, roles, change password, log out): the Profile tab of views/Profile.vue. */
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { loginRouteFor } from '../router'
import { storeToRefs } from 'pinia'
import { Button, Input, FormField, Card, DescriptionList, DescriptionItem, Alert, Link } from '@trainpaths/nb-ui'

const router = useRouter()
const authStore = useAuthStore()
const { user, authType, error, loading } = storeToRefs(authStore)

const editMode = ref(false)
const editDisplayName = ref('')
const saving = ref(false)
const successMessage = ref('')

onMounted(() => {
	authStore.fetchCurrentUser()
})

function startEdit() {
	editDisplayName.value = user.value?.displayName ?? ''
	editMode.value = true
	successMessage.value = ''
}

function cancelEdit() {
	editMode.value = false
	authStore.clearError()
}

async function saveProfile() {
	saving.value = true
	successMessage.value = ''
	try {
		const success = await authStore.updateProfile(editDisplayName.value)
		if (success) {
			editMode.value = false
			successMessage.value = 'Profile updated successfully'
			await authStore.fetchCurrentUser()
		}
	} finally {
		saving.value = false
	}
}

async function handleLogout() {
	const type = authType.value
	await authStore.logout()
	router.push(loginRouteFor(type))
}
</script>

<template>
	<div>
		<div
			v-if="loading && !user"
			class="text-black opacity-60"
		>
			Loading user info...
		</div>

		<div v-else-if="user">
			<Alert
				v-if="successMessage"
				type="success"
				class="mb-16"
			>
				{{ successMessage }}
			</Alert>

			<Card v-if="editMode">
				<form
					class="flex flex-col gap-8"
					@submit.prevent="saveProfile"
				>
					<FormField
						label="Display Name"
						:error="error || undefined"
					>
						<Input
							v-model="editDisplayName"
							:invalid="!!error"
						/>
					</FormField>
					<div class="flex gap-8">
						<Button
							type="submit"
							:loading="saving"
						>
							{{ saving ? 'Saving...' : 'Save' }}
						</Button>
						<Button
							type="button"
							variant="outline"
							text="primary"
							@click="cancelEdit"
						>
							Cancel
						</Button>
					</div>
				</form>
			</Card>

			<Card v-else>
				<DescriptionList>
					<DescriptionItem label="Email">{{ user.email }}</DescriptionItem>
					<DescriptionItem label="Display Name">
						{{ user.displayName ?? '—' }}
						<Button
							variant="ghost"
							size="sm"
							class="ml-8"
							@click="startEdit"
						>
							Edit
						</Button>
					</DescriptionItem>
					<DescriptionItem label="Type">{{ authType }}</DescriptionItem>
					<DescriptionItem label="User Type">{{ user.userType ?? '—' }}</DescriptionItem>
					<DescriptionItem label="Roles">{{ user.roles?.join(', ') || '—' }}</DescriptionItem>
					<DescriptionItem label="Organization">{{ user.organizationId ?? '—' }}</DescriptionItem>
				</DescriptionList>
			</Card>

			<div class="flex items-center gap-16 mt-16">
				<Link to="/change-password">Change Password</Link>
				<Button @click="handleLogout">Logout</Button>
			</div>
		</div>

		<div v-else>
			<p>Could not load user info.</p>
			<Alert
				v-if="error"
				type="error"
				class="mt-8"
			>
				{{ error }}
			</Alert>
		</div>
	</div>
</template>
